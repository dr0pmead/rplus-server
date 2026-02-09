using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPlusGrpc.Access;
using System.Security.Claims;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/lists")]
[Authorize]
public sealed class MetaListsController : ControllerBase
{
    private readonly IMetaDbContext _db;
    private readonly AccessService.AccessServiceClient _accessClient;

    public MetaListsController(IMetaDbContext db, AccessService.AccessServiceClient accessClient)
    {
        _db = db;
        _accessClient = accessClient;
    }

    [HttpGet]
    [RequiresPermission("meta.lists.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var lists = await _db.Lists
            .OrderBy(x => x.Title)
            .ToListAsync(ct);

        var touched = false;
        foreach (var list in lists)
        {
            if (!list.EntityTypeId.HasValue)
            {
                await EnsureListEntityTypeAsync(list, ct);
                touched = true;
                continue;
            }

            var hasName = await _db.FieldDefinitions
                .AsNoTracking()
                .AnyAsync(x => x.EntityTypeId == list.EntityTypeId.Value && x.Key == "name" && x.IsActive, ct);

            if (!hasName)
            {
                await EnsureListEntityTypeAsync(list, ct);
                touched = true;
            }
        }

        if (touched)
            await _db.SaveChangesAsync(ct);

        var payload = lists
            .OrderBy(x => x.Title)
            .Select(x => new
            {
                x.Id,
                x.EntityTypeId,
                x.Key,
                x.Title,
                x.Description,
                x.SyncMode,
                x.IsSystem,
                x.IsActive,
                x.CreatedAt
            })
            .ToList();

        return Ok(payload);
    }

    [HttpGet("by-key/{key}")]
    [RequiresPermission("meta.lists.read")]
    public async Task<IActionResult> GetByKey(string key, CancellationToken ct)
    {
        var normalized = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest(new { error = "invalid_key" });

        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Key == normalized, ct);

        if (list == null)
            return NotFound(new { error = "not_found" });

        var touched = false;
        if (!list.EntityTypeId.HasValue)
        {
            await EnsureListEntityTypeAsync(list, ct);
            touched = true;
        }
        else
        {
            var hasName = await _db.FieldDefinitions
                .AsNoTracking()
                .AnyAsync(x => x.EntityTypeId == list.EntityTypeId.Value && x.Key == "name" && x.IsActive, ct);

            if (!hasName)
            {
                await EnsureListEntityTypeAsync(list, ct);
                touched = true;
            }
        }

        if (touched)
            await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            list.Id,
            list.EntityTypeId,
            list.Key,
            list.Title,
            list.Description,
            list.SyncMode,
            list.IsSystem,
            list.IsActive,
            list.CreatedAt
        });
    }

    [HttpGet("{id:guid}/items")]
    [RequiresPermission("meta.lists.read")]
    public async Task<IActionResult> ListItems(Guid id, [FromQuery] Guid? orgNodeId, CancellationToken ct)
    {
        var exists = await _db.Lists.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!exists)
            return NotFound(new { error = "not_found" });

        var query = _db.ListItems.AsNoTracking().Where(x => x.ListId == id);

        // Filter by org node: items without binding OR items bound to specified node
        if (orgNodeId.HasValue)
            query = query.Where(x => x.OrganizationNodeId == null || x.OrganizationNodeId == orgNodeId.Value);

        var items = await query
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Title)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Title,
                x.ValueJson,
                x.ExternalId,
                x.OrganizationNodeId,
                x.IsActive,
                x.Order,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequiresPermission("meta.lists.manage")]
    public async Task<IActionResult> Create([FromBody] UpsertMetaListRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "invalid_key" });

        if (await _db.Lists.AnyAsync(x => x.Key == key, ct))
            return Conflict(new { error = "list_exists" });

        var list = new MetaList
        {
            Id = Guid.NewGuid(),
            Key = key,
            Title = NormalizeTitle(request.Title),
            Description = NormalizeOptional(request.Description, 1024),
            SyncMode = NormalizeSyncMode(request.SyncMode),
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Lists.Add(list);
        await EnsureListEntityTypeAsync(list, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(new { list.Id });
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("meta.lists.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertMetaListRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (list == null)
            return NotFound(new { error = "not_found" });

        if (list.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_list" });

        var keyChanged = false;
        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            var key = NormalizeKey(request.Key);
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "invalid_key" });

            if (!string.Equals(list.Key, key, StringComparison.OrdinalIgnoreCase)
                && await _db.Lists.AnyAsync(x => x.Key == key, ct))
                return Conflict(new { error = "list_exists" });

            keyChanged = !string.Equals(list.Key, key, StringComparison.OrdinalIgnoreCase);
            list.Key = key;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            list.Title = NormalizeTitle(request.Title);

        list.Description = NormalizeOptional(request.Description, 1024);
        if (!string.IsNullOrWhiteSpace(request.SyncMode))
            list.SyncMode = NormalizeSyncMode(request.SyncMode);

        if (request.IsActive.HasValue)
            list.IsActive = request.IsActive.Value;

        if (!list.IsSystem && (keyChanged || !string.IsNullOrWhiteSpace(request.Title) || list.EntityTypeId == null))
        {
            await EnsureListEntityTypeAsync(list, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("meta.lists.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (list == null)
            return NotFound(new { error = "not_found" });

        if (list.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_list" });

        var items = await _db.ListItems.Where(x => x.ListId == id).ToListAsync(ct);
        if (items.Count > 0)
            _db.ListItems.RemoveRange(items);

        if (list.EntityTypeId.HasValue)
        {
            var fields = await _db.FieldDefinitions.Where(x => x.EntityTypeId == list.EntityTypeId.Value).ToListAsync(ct);
            if (fields.Count > 0)
                _db.FieldDefinitions.RemoveRange(fields);

            var entityType = await _db.EntityTypes.FirstOrDefaultAsync(x => x.Id == list.EntityTypeId.Value, ct);
            if (entityType != null)
                _db.EntityTypes.Remove(entityType);
        }

        _db.Lists.Remove(list);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/items")]
    [RequiresPermission("meta.lists.manage")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] UpsertMetaListItemRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (list == null)
            return NotFound(new { error = "not_found" });
        if (list.IsSystem && !await IsSystemListItemsEditableAsync(list, ct))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_list" });

        var code = NormalizeKey(request.Code);
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_code" });

        if (await _db.ListItems.AnyAsync(x => x.ListId == id && x.Code == code, ct))
            return Conflict(new { error = "item_exists" });

        var valueJson = NormalizeJson(request.ValueJson ?? request.FieldsJson);
        var title = NormalizeTitle(request.Title);
        if (title == "Untitled" && !string.IsNullOrWhiteSpace(valueJson))
        {
            var titleFromJson = TryExtractName(valueJson);
            if (!string.IsNullOrWhiteSpace(titleFromJson))
                title = NormalizeTitle(titleFromJson);
        }

        var validation = await ValidateListItemAsync(list, valueJson, title, ct);
        if (!validation.Ok)
            return BadRequest(new { error = validation.Error, field = validation.Field });

        var item = new MetaListItem
        {
            Id = Guid.NewGuid(),
            ListId = id,
            Code = code,
            Title = title,
            ValueJson = valueJson,
            ExternalId = NormalizeOptional(request.ExternalId, 128),
            OrganizationNodeId = request.OrganizationNodeId,
            IsActive = request.IsActive ?? true,
            Order = request.Order ?? 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.ListItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return Ok(new { item.Id });
    }

    [HttpPut("items/{itemId:guid}")]
    [RequiresPermission("meta.lists.manage")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpsertMetaListItemRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var item = await _db.ListItems.FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item == null)
            return NotFound(new { error = "not_found" });
        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Id == item.ListId, ct);
        if (list == null)
            return NotFound(new { error = "not_found" });
        if (list.IsSystem && !await IsSystemListItemsEditableAsync(list, ct))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_list" });

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var code = NormalizeKey(request.Code);
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { error = "invalid_code" });

            if (!string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && await _db.ListItems.AnyAsync(x => x.ListId == item.ListId && x.Code == code, ct))
                return Conflict(new { error = "item_exists" });

            item.Code = code;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            item.Title = NormalizeTitle(request.Title);

        var nextJson = NormalizeJson(request.ValueJson ?? request.FieldsJson, item.ValueJson);
        item.ValueJson = nextJson;
        item.ExternalId = NormalizeOptional(request.ExternalId, 128);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            var nameFromJson = TryExtractName(item.ValueJson ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(nameFromJson))
                item.Title = NormalizeTitle(nameFromJson);
        }

        var validation = await ValidateListItemAsync(list, item.ValueJson, item.Title, ct);
        if (!validation.Ok)
            return BadRequest(new { error = validation.Error, field = validation.Field });

        if (request.IsActive.HasValue)
            item.IsActive = request.IsActive.Value;

        if (request.Order.HasValue)
            item.Order = request.Order.Value;

        // Update org node binding if provided (null clears binding)
        if (request.OrganizationNodeId.HasValue || request.OrganizationNodeId == null)
            item.OrganizationNodeId = request.OrganizationNodeId;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("items/{itemId:guid}")]
    [RequiresPermission("meta.lists.manage")]
    public async Task<IActionResult> DeleteItem(Guid itemId, CancellationToken ct)
    {
        var item = await _db.ListItems.FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item == null)
            return NotFound(new { error = "not_found" });
        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Id == item.ListId, ct);
        if (list == null)
            return NotFound(new { error = "not_found" });
        if (list.IsSystem && !await IsSystemListItemsEditableAsync(list, ct))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_list" });

        _db.ListItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/sync")]
    [RequiresPermission("meta.lists.sync")]
    public async Task<IActionResult> Sync(Guid id, [FromBody] SyncListRequest request, CancellationToken ct)
    {
        var list = await _db.Lists.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (list == null)
            return NotFound(new { error = "not_found" });
        if (list.IsSystem && !await IsSystemListItemsEditableAsync(list, ct))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_list" });

        if (request?.Items == null)
            return BadRequest(new { error = "invalid_request" });

        var existing = await _db.ListItems.Where(x => x.ListId == id).ToListAsync(ct);
        var existingByCode = existing.ToDictionary(x => x.Code, x => x);

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var code = NormalizeKey(item.Code);
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { error = "invalid_code", index = i });

            if (!existingByCode.TryGetValue(code, out var target))
            {
                target = new MetaListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = id,
                    Code = code,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ListItems.Add(target);
            }

            target.Title = NormalizeTitle(item.Title);
            target.ValueJson = NormalizeJson(item.ValueJson, target.ValueJson);
            target.ExternalId = NormalizeOptional(item.ExternalId, 128);
            target.OrganizationNodeId = item.OrganizationNodeId;
            target.IsActive = item.IsActive ?? true;
            target.Order = item.Order ?? target.Order;

            var validation = await ValidateListItemAsync(list, target.ValueJson, target.Title, ct);
            if (!validation.Ok)
                return BadRequest(new { error = validation.Error, field = validation.Field, index = i });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string NormalizeKey(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeTitle(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        return s.Length == 0 ? "Untitled" : s;
    }

    private static string NormalizeSyncMode(string? value)
    {
        var s = (value ?? "manual").Trim().ToLowerInvariant();
        return s.Length == 0 ? "manual" : s;
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    private static string? NormalizeJson(string? json, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            using var _ = JsonDocument.Parse(json);
            return json.Trim();
        }
        catch
        {
            return fallback;
        }
    }

    public sealed record UpsertMetaListRequest
    {
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? SyncMode { get; init; }
        public bool? IsActive { get; init; }
    }

    public sealed record UpsertMetaListItemRequest
    {
        public string? Code { get; init; }
        public string? Title { get; init; }
        public string? ValueJson { get; init; }
        public string? FieldsJson { get; init; }
        public string? ExternalId { get; init; }
        public Guid? OrganizationNodeId { get; init; }
        public bool? IsActive { get; init; }
        public int? Order { get; init; }
    }

    public sealed record SyncListRequest
    {
        public List<SyncListItem> Items { get; init; } = new();
    }

    public sealed record SyncListItem
    {
        public string? Code { get; init; }
        public string? Title { get; init; }
        public string? ValueJson { get; init; }
        public string? ExternalId { get; init; }
        public Guid? OrganizationNodeId { get; init; }
        public bool? IsActive { get; init; }
        public int? Order { get; init; }
    }

    private async Task EnsureListEntityTypeAsync(MetaList list, CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(list.Key);
        var entityKey = $"list:{normalizedKey}";

        MetaEntityType? entity = null;
        if (list.EntityTypeId.HasValue)
        {
            entity = await _db.EntityTypes.FirstOrDefaultAsync(x => x.Id == list.EntityTypeId.Value, ct);
        }

        if (entity == null)
        {
            entity = await _db.EntityTypes.FirstOrDefaultAsync(x => x.Key == entityKey, ct);
        }

        if (entity == null)
        {
            entity = new MetaEntityType
            {
                Id = Guid.NewGuid(),
                Key = entityKey,
                Title = list.Title,
                Description = list.Description,
                IsSystem = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.EntityTypes.Add(entity);
        }
        else
        {
            if (!string.Equals(entity.Key, entityKey, StringComparison.OrdinalIgnoreCase))
                entity.Key = entityKey;
            entity.Title = list.Title;
            entity.Description = list.Description;
            entity.IsActive = list.IsActive;
        }

        list.EntityTypeId = entity.Id;

        var nameField = await _db.FieldDefinitions.FirstOrDefaultAsync(x => x.EntityTypeId == entity.Id && x.Key == "name", ct);
        if (nameField == null)
        {
            _db.FieldDefinitions.Add(new MetaFieldDefinition
            {
                Id = Guid.NewGuid(),
                EntityTypeId = entity.Id,
                Key = "name",
                Title = "Name",
                DataType = "text",
                Order = 0,
                IsRequired = true,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var legacyTitleField = await _db.FieldDefinitions.FirstOrDefaultAsync(x => x.EntityTypeId == entity.Id && x.Key == "title", ct);
        if (legacyTitleField != null && legacyTitleField.IsSystem)
        {
            legacyTitleField.IsActive = false;
            legacyTitleField.IsRequired = false;
        }
    }

    private static string? TryExtractName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (doc.RootElement.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                return nameEl.GetString();

            if (doc.RootElement.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
                return titleEl.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<bool> IsSystemListItemsEditableAsync(MetaList list, CancellationToken ct)
    {
        // Root users can edit any system list
        if (await IsRootUserAsync(ct))
            return true;
        // Special case: loyalty_levels is editable by admins
        return list.IsSystem && string.Equals(list.Key, "loyalty_levels", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsRootUserAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var tenantIdClaim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId");
        var tenantId = Guid.TryParse(tenantIdClaim, out var tid) ? tid : Guid.Empty;

        try
        {
            var response = await _accessClient.CheckPermissionAsync(new CheckPermissionRequest
            {
                UserId = userId,
                TenantId = tenantId.ToString(),
                PermissionId = "*",
                ApplicationId = "meta"
            }, cancellationToken: ct);
            return response.IsAllowed;
        }
        catch
        {
            return false;
        }
    }

    private async Task<ListItemValidationResult> ValidateListItemAsync(MetaList list, string? valueJson, string? title, CancellationToken ct)
    {
        if (!list.EntityTypeId.HasValue)
            return ListItemValidationResult.OkResult();

        var fields = await _db.FieldDefinitions
            .AsNoTracking()
            .Where(x => x.EntityTypeId == list.EntityTypeId.Value && x.IsActive)
            .ToListAsync(ct);

        if (fields.Count == 0)
            return ListItemValidationResult.OkResult();

        JsonElement? root = null;
        if (!string.IsNullOrWhiteSpace(valueJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(valueJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    root = doc.RootElement.Clone();
            }
            catch
            {
                return new ListItemValidationResult(false, "invalid_json", null);
            }
        }

        var listItemsCache = new Dictionary<Guid, HashSet<string>>();

        foreach (var field in fields)
        {
            var options = ParseFieldOptions(field);
            var hasValue = TryGetValue(root, field.Key, out var valueEl);
            if (!hasValue && string.Equals(field.Key, "name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(title))
                hasValue = true;

            if (field.IsRequired && !hasValue)
                return new ListItemValidationResult(false, "required_field", field.Key);

            if (field.IsRequired && hasValue && IsEmptyValue(valueEl) &&
                !(string.Equals(field.Key, "name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(title)))
            {
                return new ListItemValidationResult(false, "required_field", field.Key);
            }

            if (!hasValue)
                continue;

            var validation = ValidateFieldValue(field, options, valueEl, listItemsCache);
            if (!validation.Ok)
                return validation with { Field = field.Key };
        }

        return ListItemValidationResult.OkResult();
    }

    private static bool TryGetValue(JsonElement? root, string key, out JsonElement value)
    {
        value = default;
        if (root == null || root.Value.ValueKind != JsonValueKind.Object)
            return false;

        if (root.Value.TryGetProperty(key, out var prop))
        {
            value = prop;
            return true;
        }

        return false;
    }

    private static bool IsEmptyValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() == 0,
            _ => false
        };
    }

    private ListItemValidationResult ValidateFieldValue(
        MetaFieldDefinition field,
        FieldOptions options,
        JsonElement valueEl,
        Dictionary<Guid, HashSet<string>> listItemsCache)
    {
        if (options.Multiple)
        {
            if (valueEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueEl.EnumerateArray())
                {
                    var res = ValidateSingleValue(field, options, item, listItemsCache);
                    if (!res.Ok)
                        return res;
                }
                return ListItemValidationResult.OkResult();
            }

            // Allow single value for backward compatibility
            return ValidateSingleValue(field, options, valueEl, listItemsCache);
        }

        return ValidateSingleValue(field, options, valueEl, listItemsCache);
    }

    private ListItemValidationResult ValidateSingleValue(
        MetaFieldDefinition field,
        FieldOptions options,
        JsonElement valueEl,
        Dictionary<Guid, HashSet<string>> listItemsCache)
    {
        if (valueEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return ListItemValidationResult.OkResult();

        var dataType = (field.DataType ?? "text").Trim().ToLowerInvariant();
        switch (dataType)
        {
            case "text":
            case "string":
            case "textarea":
                if (valueEl.ValueKind != JsonValueKind.String)
                    return new ListItemValidationResult(false, "invalid_type", null);
                break;
            case "number":
            case "int":
            case "float":
                if (valueEl.ValueKind != JsonValueKind.Number)
                    return new ListItemValidationResult(false, "invalid_type", null);
                break;
            case "boolean":
                if (valueEl.ValueKind != JsonValueKind.True && valueEl.ValueKind != JsonValueKind.False)
                    return new ListItemValidationResult(false, "invalid_type", null);
                break;
            case "date":
            case "datetime":
                if (valueEl.ValueKind != JsonValueKind.String || !DateTime.TryParse(valueEl.GetString(), out _))
                    return new ListItemValidationResult(false, "invalid_type", null);
                break;
            case "select":
                if (valueEl.ValueKind != JsonValueKind.String)
                    return new ListItemValidationResult(false, "invalid_type", null);
                var selectValue = valueEl.GetString() ?? string.Empty;
                if (options.Values != null && options.Values.Count > 0 && !options.Values.Contains(selectValue))
                    return new ListItemValidationResult(false, "invalid_value", null);
                if (options.ReferenceListId.HasValue)
                {
                    var allowed = GetListItemCodes(options.ReferenceListId.Value, listItemsCache);
                    if (!allowed.Contains(selectValue))
                        return new ListItemValidationResult(false, "invalid_value", null);
                }
                break;
            case "json":
                break;
            default:
                if (valueEl.ValueKind != JsonValueKind.String)
                    return new ListItemValidationResult(false, "invalid_type", null);
                break;
        }

        return ListItemValidationResult.OkResult();
    }

    private HashSet<string> GetListItemCodes(Guid listId, Dictionary<Guid, HashSet<string>> cache)
    {
        if (cache.TryGetValue(listId, out var cached))
            return cached;

        var codes = _db.ListItems.AsNoTracking()
            .Where(x => x.ListId == listId && x.IsActive)
            .Select(x => x.Code)
            .ToList();

        var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        cache[listId] = set;
        return set;
    }

    private static FieldOptions ParseFieldOptions(MetaFieldDefinition field)
    {
        var multiple = false;
        HashSet<string>? values = null;
        Guid? listId = null;

        if (!string.IsNullOrWhiteSpace(field.OptionsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(field.OptionsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("behavior", out var behaviorEl) && behaviorEl.ValueKind == JsonValueKind.Object)
                    {
                        if (behaviorEl.TryGetProperty("multiple", out var behaviorMultEl) &&
                            (behaviorMultEl.ValueKind == JsonValueKind.True || behaviorMultEl.ValueKind == JsonValueKind.False))
                        {
                            multiple = behaviorMultEl.GetBoolean();
                        }
                    }

                    if (doc.RootElement.TryGetProperty("multiple", out var multEl) &&
                        (multEl.ValueKind == JsonValueKind.True || multEl.ValueKind == JsonValueKind.False))
                    {
                        multiple = multEl.GetBoolean();
                    }

                    if (doc.RootElement.TryGetProperty("values", out var valuesEl) && valuesEl.ValueKind == JsonValueKind.Array)
                    {
                        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var item in valuesEl.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                items.Add(item.GetString() ?? string.Empty);
                        }
                        values = items;
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(field.ReferenceSourceJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(field.ReferenceSourceJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("listId", out var listEl))
                {
                    if (Guid.TryParse(listEl.GetString(), out var parsed))
                        listId = parsed;
                }
            }
            catch { }
        }

        return new FieldOptions(multiple, values, listId);
    }

    private sealed record FieldOptions(bool Multiple, HashSet<string>? Values, Guid? ReferenceListId);

    private sealed record ListItemValidationResult(bool Ok, string? Error, string? Field)
    {
        public static ListItemValidationResult OkResult() => new(true, null, null);
    }
}
