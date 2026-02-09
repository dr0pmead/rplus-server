using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.Meta.Api.Validation;
using RPlus.SDK.Access.Authorization;
using System.Text.Json;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/fields")]
[Authorize]
public sealed class MetaFieldsController : ControllerBase
{
    private readonly IMetaDbContext _db;

    public MetaFieldsController(IMetaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("meta.fields.read")]
    public async Task<IActionResult> List([FromQuery] Guid? entityTypeId, CancellationToken ct)
    {
        var query = _db.FieldDefinitions.AsNoTracking();
        if (entityTypeId.HasValue && entityTypeId.Value != Guid.Empty)
            query = query.Where(x => x.EntityTypeId == entityTypeId.Value);

        var items = await query
            .OrderBy(x => x.EntityTypeId)
            .ThenBy(x => x.Order)
            .Select(x => new
            {
                x.Id,
                x.EntityTypeId,
                x.Key,
                x.Title,
                x.DataType,
                x.Order,
                x.IsRequired,
                x.IsSystem,
                x.IsActive,
                x.OptionsJson,
                x.ValidationJson,
                x.ReferenceSourceJson,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequiresPermission("meta.fields.manage")]
    public async Task<IActionResult> Create([FromBody] UpsertMetaFieldRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        if (request.EntityTypeId == Guid.Empty)
            return BadRequest(new { error = "invalid_entity_type" });

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "invalid_key" });

        if (!await _db.EntityTypes.AnyAsync(x => x.Id == request.EntityTypeId, ct))
            return BadRequest(new { error = "entity_type_not_found" });

        if (await _db.FieldDefinitions.AnyAsync(x => x.EntityTypeId == request.EntityTypeId && x.Key == key, ct))
            return Conflict(new { error = "field_exists" });

        var now = DateTime.UtcNow;
        var field = new MetaFieldDefinition
        {
            Id = Guid.NewGuid(),
            EntityTypeId = request.EntityTypeId,
            Key = key,
            Title = NormalizeTitle(request.Title),
            DataType = NormalizeType(request.DataType),
            Order = request.Order ?? 0,
            IsRequired = request.IsRequired ?? false,
            IsSystem = false,
            IsActive = request.IsActive ?? true,
            OptionsJson = request.OptionsJson,
            ValidationJson = NormalizeJson(request.ValidationJson),
            ReferenceSourceJson = NormalizeJson(request.ReferenceSourceJson),
            CreatedAt = now
        };

        var optionsResult = MetaFieldOptionsValidator.Validate(field.OptionsJson, field.DataType);
        if (!optionsResult.IsValid)
            return BadRequest(new { error = optionsResult.Errors[0].Code, details = optionsResult.Errors });

        field.OptionsJson = optionsResult.NormalizedJson;
        if (string.Equals(field.DataType, "select", StringComparison.OrdinalIgnoreCase))
        {
            var hasSource = HasSelectSource(field.OptionsJson);
            if (!hasSource && string.IsNullOrWhiteSpace(field.ReferenceSourceJson))
                return BadRequest(new { error = "select_source_required" });
        }

        _db.FieldDefinitions.Add(field);
        await _db.SaveChangesAsync(ct);

        return Ok(new { field.Id });
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("meta.fields.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertMetaFieldRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var field = await _db.FieldDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (field == null)
            return NotFound(new { error = "not_found" });

        if (field.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_field" });

        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            var key = NormalizeKey(request.Key);
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "invalid_key" });

            if (!string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)
                && await _db.FieldDefinitions.AnyAsync(x => x.EntityTypeId == field.EntityTypeId && x.Key == key, ct))
                return Conflict(new { error = "field_exists" });

            field.Key = key;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            field.Title = NormalizeTitle(request.Title);

        if (!string.IsNullOrWhiteSpace(request.DataType))
            field.DataType = NormalizeType(request.DataType);

        if (request.Order.HasValue)
            field.Order = request.Order.Value;

        if (request.IsRequired.HasValue)
            field.IsRequired = request.IsRequired.Value;

        if (request.IsActive.HasValue)
            field.IsActive = request.IsActive.Value;

        var incomingOptions = string.IsNullOrWhiteSpace(request.OptionsJson)
            ? field.OptionsJson
            : request.OptionsJson;

        var optionsResult = MetaFieldOptionsValidator.Validate(incomingOptions, field.DataType);
        if (!optionsResult.IsValid)
            return BadRequest(new { error = optionsResult.Errors[0].Code, details = optionsResult.Errors });

        field.OptionsJson = optionsResult.NormalizedJson;
        field.ValidationJson = NormalizeJson(request.ValidationJson, field.ValidationJson);
        field.ReferenceSourceJson = NormalizeJson(request.ReferenceSourceJson, field.ReferenceSourceJson);
        if (string.Equals(field.DataType, "select", StringComparison.OrdinalIgnoreCase))
        {
            var hasSource = HasSelectSource(field.OptionsJson);
            if (!hasSource && string.IsNullOrWhiteSpace(field.ReferenceSourceJson))
                return BadRequest(new { error = "select_source_required" });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("meta.fields.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var field = await _db.FieldDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (field == null)
            return NotFound(new { error = "not_found" });

        if (field.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_field" });

        _db.FieldDefinitions.Remove(field);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeTitle(string value)
    {
        var s = value.Trim();
        return s.Length == 0 ? "Untitled" : s;
    }

    private static string NormalizeType(string? value)
    {
        var s = (value ?? "text").Trim();
        return s.Length == 0 ? "text" : s.ToLowerInvariant();
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

    private static bool HasSelectSource(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(optionsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (doc.RootElement.TryGetProperty("values", out var valuesNode) &&
                valuesNode.ValueKind == JsonValueKind.Array &&
                valuesNode.GetArrayLength() > 0)
            {
                return true;
            }

            if (doc.RootElement.TryGetProperty("behavior", out var behaviorNode) &&
                behaviorNode.ValueKind == JsonValueKind.Object &&
                behaviorNode.TryGetProperty("source", out var sourceNode) &&
                sourceNode.ValueKind == JsonValueKind.Object)
            {
                if (sourceNode.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public sealed record UpsertMetaFieldRequest
    {
        public Guid EntityTypeId { get; init; }
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? DataType { get; init; }
        public int? Order { get; init; }
        public bool? IsRequired { get; init; }
        public bool? IsActive { get; init; }
        public string? OptionsJson { get; init; }
        public string? ValidationJson { get; init; }
        public string? ReferenceSourceJson { get; init; }
    }
}
