using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Api.Models;
using RPlus.Kernel.Integration.Api.Services;
using RPlusGrpc.Meta;
using RPlus.SDK.Access.Authorization;
using System.Text.Json;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/admin/scan-fields")]
public sealed class IntegrationScanFieldsAdminController : ControllerBase
{
    private static readonly string[] AllowedGroups = { "user", "loyalty", "partner" };
    private static readonly string[] AllowedResolvers = { "profile", "loyalty_profile", "partner" };
    private static readonly string[] AllowedTypes = { "string", "number" };

    private readonly IScanFieldCatalogService _catalogService;
    private readonly MetaService.MetaServiceClient _metaClient;
    private readonly IOptionsMonitor<IntegrationMetaOptions> _metaOptions;
    private readonly ILogger<IntegrationScanFieldsAdminController> _logger;

    public IntegrationScanFieldsAdminController(
        IScanFieldCatalogService catalogService,
        MetaService.MetaServiceClient metaClient,
        IOptionsMonitor<IntegrationMetaOptions> metaOptions,
        ILogger<IntegrationScanFieldsAdminController> logger)
    {
        _catalogService = catalogService;
        _metaClient = metaClient;
        _metaOptions = metaOptions;
        _logger = logger;
    }

    [HttpGet]
    [RequiresPermission("integration.scan_fields.manage")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var (listId, items) = await LoadScanFieldItemsAsync(cancellationToken);
        var parsed = ParseMetaItems(items);

        var fields = parsed.Values
            .Where(x => x.IsCustom)
            .Select(custom => new ScanFieldAdminItem
            {
                Key = custom.Key ?? string.Empty,
                Title = custom.Title ?? custom.Key ?? string.Empty,
                Group = custom.Group ?? "meta",
                Type = NormalizeType(custom.Type) ?? "string",
                Resolver = custom.Resolver ?? string.Empty,
                ResolverConfig = ToDictionaryOrNull(custom.ResolverConfig),
                Description = custom.Description,
                SortOrder = custom.SortOrder,
                IsAdvanced = custom.IsAdvanced ?? false,
                Expose = custom.Expose ?? true,
                IsEnabled = custom.IsEnabled ?? true,
                IsCustom = true
            })
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Group)
            .ThenBy(x => x.Title)
            .ToList();

        var unknownKeys = parsed.Values
            .Where(x => !x.IsCustom)
            .Select(x => x.Key!)
            .OrderBy(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new
        {
            listId,
            items = fields,
            unknownKeys
        });
    }

    [HttpGet("sources")]
    [RequiresPermission("integration.scan_fields.manage")]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken)
    {
        var sources = await _catalogService.GetSourceCatalogAsync(cancellationToken);
        return Ok(sources);
    }

    [HttpPost]
    [RequiresPermission("integration.scan_fields.manage")]
    public async Task<IActionResult> Create([FromBody] ScanFieldAdminUpsertRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new { error = "invalid_request" });

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "invalid_key" });

        var (listId, items) = await LoadScanFieldItemsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(listId))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "scan_fields_not_configured" });

        if (items.Any(x => NormalizeKey(x.Code) == key))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "already_exists" });

        var validation = ValidateCustomRequest(request);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Error });

        var customPayload = SerializeCustom(request, key);
        var upsertCustom = new UpsertListItemsRequest
        {
            ListId = listId,
            Strict = true
        };
        upsertCustom.Items.Add(new UpsertListItem
        {
            ExternalId = key,
            Code = key,
            Title = request.Title ?? key,
            ValueJson = customPayload,
            IsActive = request.IsEnabled ?? true,
            Order = request.SortOrder ?? 0
        });

        await _metaClient.UpsertListItemsAsync(upsertCustom, BuildMetadata(), cancellationToken: cancellationToken);
        return Ok(new { success = true, mode = "custom" });
    }

    [HttpPut("{key}")]
    [RequiresPermission("integration.scan_fields.manage")]
    public async Task<IActionResult> Update(string key, [FromBody] ScanFieldAdminUpsertRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var normalized = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest(new { error = "invalid_key" });

        var (listId, items) = await LoadScanFieldItemsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(listId))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "scan_fields_not_configured" });

        var existingItem = items.FirstOrDefault(x => NormalizeKey(x.Code) == normalized);
        if (existingItem == null)
            return NotFound(new { error = "not_found" });

        var existingMeta = ReadMetaItem(existingItem);
        if (existingMeta == null || !existingMeta.IsCustom)
            return BadRequest(new { error = "invalid_item" });

        var merged = MergeCustom(request, existingMeta, normalized);
        var validation = ValidateCustomRequest(merged);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Error });

        var customPayload = SerializeCustom(merged, normalized);
        var upsertCustom = new UpsertListItemsRequest
        {
            ListId = listId,
            Strict = true
        };
        upsertCustom.Items.Add(new UpsertListItem
        {
            ExternalId = normalized,
            Code = normalized,
            Title = merged.Title ?? normalized,
            ValueJson = customPayload,
            IsActive = merged.IsEnabled ?? true,
            Order = merged.SortOrder ?? existingItem.Order
        });

        await _metaClient.UpsertListItemsAsync(upsertCustom, BuildMetadata(), cancellationToken: cancellationToken);
        return Ok(new { success = true, mode = "custom" });
    }

    [HttpDelete("{key}")]
    [RequiresPermission("integration.scan_fields.manage")]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        var normalized = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest(new { error = "invalid_key" });

        var (listId, _) = await LoadScanFieldItemsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(listId))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "scan_fields_not_configured" });

        var delete = new DeleteListItemsRequest { ListId = listId };
        delete.ExternalId.Add(normalized);
        await _metaClient.DeleteListItemsAsync(delete, BuildMetadata(), cancellationToken: cancellationToken);

        return Ok(new { success = true });
    }

    [HttpDelete("unknown")]
    [RequiresPermission("integration.scan_fields.manage")]
    public async Task<IActionResult> DeleteUnknown(CancellationToken cancellationToken)
    {
        var (listId, items) = await LoadScanFieldItemsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(listId))
            return StatusCode(StatusCodes.Status409Conflict, new { error = "scan_fields_not_configured" });

        var unknown = items
            .Select(ReadMetaItem)
            .Where(x => x is { IsCustom: false } && !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => x!.Key!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknown.Count == 0)
            return Ok(new { deleted = Array.Empty<string>() });

        var delete = new DeleteListItemsRequest { ListId = listId };
        delete.ExternalId.AddRange(unknown);
        await _metaClient.DeleteListItemsAsync(delete, BuildMetadata(), cancellationToken: cancellationToken);

        return Ok(new { deleted = unknown });
    }

    private async Task<(string? ListId, List<MetaListItem> Items)> LoadScanFieldItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var listResponse = await _metaClient.GetListByKeyAsync(
                new GetListByKeyRequest { Key = "scan_fields" },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            if (!listResponse.Found)
                return (null, new List<MetaListItem>());

            var listItems = await _metaClient.GetListItemsAsync(
                new GetListItemsRequest { ListId = listResponse.List.Id },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            return (listResponse.List.Id, listItems.Items.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load scan_fields list items from Meta");
            return (null, new List<MetaListItem>());
        }
    }

    private static Dictionary<string, ScanFieldMetaItem> ParseMetaItems(IEnumerable<MetaListItem> items)
    {
        var result = new Dictionary<string, ScanFieldMetaItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var parsed = ReadMetaItem(item);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Key))
                continue;

            result[parsed.Key!] = parsed;
        }

        return result;
    }

    private static (bool IsValid, string? Error) ValidateCustomRequest(ScanFieldAdminUpsertRequest request)
    {
        var resolver = NormalizeResolver(request.Resolver);
        if (string.IsNullOrWhiteSpace(resolver))
            return (false, "invalid_resolver");

        var group = NormalizeGroup(request.Group);
        if (string.IsNullOrWhiteSpace(group))
            return (false, "invalid_group");

        var type = NormalizeType(request.Type);
        if (string.IsNullOrWhiteSpace(type))
            return (false, "invalid_type");

        if (request.SortOrder.HasValue && request.SortOrder.Value is < -10000 or > 10000)
            return (false, "invalid_sort_order");

        if (request.Title is { Length: 0 })
            return (false, "invalid_title");

        var config = request.ResolverConfig ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (resolver is "profile" or "loyalty_profile" or "partner")
        {
            if (!config.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
                return (false, "invalid_resolver_config");
        }

        return (true, null);
    }

    private static ScanFieldAdminUpsertRequest MergeCustom(
        ScanFieldAdminUpsertRequest request,
        ScanFieldMetaItem existing,
        string key)
    {
        return new ScanFieldAdminUpsertRequest
        {
            Key = key,
            Title = request.Title ?? existing.Title ?? key,
            Group = request.Group ?? existing.Group,
            Type = request.Type ?? existing.Type,
            Resolver = request.Resolver ?? existing.Resolver,
            ResolverConfig = request.ResolverConfig ?? existing.ResolverConfig,
            Description = request.Description ?? existing.Description,
            SortOrder = request.SortOrder ?? existing.SortOrder,
            IsAdvanced = request.IsAdvanced ?? existing.IsAdvanced,
            IsEnabled = request.IsEnabled ?? existing.IsEnabled,
            Expose = request.Expose ?? existing.Expose
        };
    }

    private static string SerializeCustom(ScanFieldAdminUpsertRequest request, string key)
    {
        var payload = new
        {
            title = request.Title ?? key,
            group = NormalizeGroup(request.Group) ?? "meta",
            type = NormalizeType(request.Type),
            resolver = NormalizeResolver(request.Resolver),
            resolverConfig = request.ResolverConfig ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            description = request.Description,
            sortOrder = request.SortOrder,
            isAdvanced = request.IsAdvanced,
            isEnabled = request.IsEnabled,
            expose = request.Expose
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static ScanFieldMetaItem? ReadMetaItem(MetaListItem item)
    {
        var key = NormalizeKey(item.Code);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (string.IsNullOrWhiteSpace(item.ValueJson))
        {
            return new ScanFieldMetaItem
            {
                Key = key,
                Title = item.Title,
                IsEnabled = item.IsActive,
                IsCustom = false
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(item.ValueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var root = doc.RootElement;
            var resolver = ReadString(root, "resolver");
            var resolverConfig = ReadDictionary(root, "resolverConfig");
            var hasResolver = !string.IsNullOrWhiteSpace(resolver);

            return new ScanFieldMetaItem
            {
                Key = key,
                Title = ReadString(root, "title") ?? item.Title,
                Group = ReadString(root, "group"),
                Type = ReadString(root, "type"),
                Resolver = resolver,
                ResolverConfig = resolverConfig,
                Description = ReadString(root, "description"),
                SortOrder = ReadInt(root, "sortOrder"),
                IsAdvanced = ReadBool(root, "isAdvanced"),
                IsEnabled = ReadBool(root, "isEnabled") ?? item.IsActive,
                Expose = ReadBool(root, "expose"),
                IsCustom = hasResolver
            };
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ReadDictionary(JsonElement root, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in value.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                result[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }
            else
            {
                result[prop.Name] = prop.Value.GetRawText();
            }
        }

        return result;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
            return result;
        return null;
    }

    private static bool? ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string NormalizeKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static Dictionary<string, string>? ToDictionaryOrNull(IReadOnlyDictionary<string, string>? source)
    {
        if (source == null || source.Count == 0)
            return null;

        return new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeGroup(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedGroups.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeType(string? value)
    {
        var normalized = (value ?? "string").Trim().ToLowerInvariant();
        return AllowedTypes.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeResolver(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedResolvers.Contains(normalized) ? normalized : null;
    }

    private Metadata? BuildMetadata()
    {
        var secret = _metaOptions.CurrentValue.ServiceSecret;
        if (!string.IsNullOrWhiteSpace(secret))
            return new Metadata { { "x-rplus-service-secret", secret.Trim() } };
        return null;
    }

    private sealed class ScanFieldMetaItem
    {
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? Group { get; init; }
        public string? Type { get; init; }
        public string? Resolver { get; init; }
        public Dictionary<string, string> ResolverConfig { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Description { get; init; }
        public int? SortOrder { get; init; }
        public bool? IsAdvanced { get; init; }
        public bool? IsEnabled { get; init; }
        public bool? Expose { get; init; }
        public bool IsCustom { get; init; }
    }
}
