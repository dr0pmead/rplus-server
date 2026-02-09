using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System.Text.Json;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/field-types")]
[Authorize]
public sealed class MetaFieldTypesController : ControllerBase
{
    private readonly IMetaDbContext _db;

    public MetaFieldTypesController(IMetaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("meta.fields.read")]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct)
    {
        var query = _db.FieldTypes.AsNoTracking();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.Title)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.Title,
                x.Description,
                x.UiSchemaJson,
                x.IsSystem,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequiresPermission("meta.fields.manage")]
    public async Task<IActionResult> Create([FromBody] UpsertMetaFieldTypeRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "invalid_key" });

        if (await _db.FieldTypes.AnyAsync(x => x.Key == key, ct))
            return Conflict(new { error = "type_exists" });

        var type = new MetaFieldType
        {
            Id = Guid.NewGuid(),
            Key = key,
            Title = NormalizeTitle(request.Title),
            Description = request.Description?.Trim(),
            UiSchemaJson = NormalizeJson(request.UiSchemaJson),
            IsSystem = false,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow
        };

        _db.FieldTypes.Add(type);
        await _db.SaveChangesAsync(ct);
        return Ok(new { type.Id });
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("meta.fields.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertMetaFieldTypeRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var type = await _db.FieldTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (type == null)
            return NotFound(new { error = "not_found" });

        if (type.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_type" });

        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            var key = NormalizeKey(request.Key);
            if (!string.Equals(type.Key, key, StringComparison.OrdinalIgnoreCase)
                && await _db.FieldTypes.AnyAsync(x => x.Key == key, ct))
                return Conflict(new { error = "type_exists" });

            type.Key = key;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            type.Title = NormalizeTitle(request.Title);

        if (request.Description != null)
            type.Description = request.Description?.Trim();

        if (request.IsActive.HasValue)
            type.IsActive = request.IsActive.Value;

        type.UiSchemaJson = NormalizeJson(request.UiSchemaJson, type.UiSchemaJson);

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("meta.fields.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var type = await _db.FieldTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (type == null)
            return NotFound(new { error = "not_found" });

        if (type.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_type" });

        _db.FieldTypes.Remove(type);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeTitle(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        return s.Length == 0 ? "Untitled" : s;
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

    public sealed record UpsertMetaFieldTypeRequest
    {
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? UiSchemaJson { get; init; }
        public bool? IsActive { get; init; }
    }
}
