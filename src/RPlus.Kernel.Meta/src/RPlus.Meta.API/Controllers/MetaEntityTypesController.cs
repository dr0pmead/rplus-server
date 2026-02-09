using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/entity-types")]
[Authorize]
public sealed class MetaEntityTypesController : ControllerBase
{
    private readonly IMetaDbContext _db;

    public MetaEntityTypesController(IMetaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("meta.entity_types.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.EntityTypes.AsNoTracking()
            .OrderBy(x => x.Title)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.Title,
                x.Description,
                x.IsSystem,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequiresPermission("meta.entity_types.manage")]
    public async Task<IActionResult> Create([FromBody] UpsertMetaEntityTypeRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "invalid_key" });

        if (await _db.EntityTypes.AnyAsync(x => x.Key == key, ct))
            return Conflict(new { error = "entity_type_exists" });

        var now = DateTime.UtcNow;
        var entityType = new MetaEntityType
        {
            Id = Guid.NewGuid(),
            Key = key,
            Title = NormalizeTitle(request.Title),
            Description = NormalizeOptional(request.Description, 1024),
            IsSystem = false,
            IsActive = true,
            CreatedAt = now
        };

        _db.EntityTypes.Add(entityType);
        await _db.SaveChangesAsync(ct);

        return Ok(new { entityType.Id });
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("meta.entity_types.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertMetaEntityTypeRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var entityType = await _db.EntityTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entityType == null)
            return NotFound(new { error = "not_found" });

        if (entityType.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_entity_type" });

        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            var key = NormalizeKey(request.Key);
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "invalid_key" });

            if (!string.Equals(entityType.Key, key, StringComparison.OrdinalIgnoreCase)
                && await _db.EntityTypes.AnyAsync(x => x.Key == key, ct))
                return Conflict(new { error = "entity_type_exists" });

            entityType.Key = key;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            entityType.Title = NormalizeTitle(request.Title);

        entityType.Description = NormalizeOptional(request.Description, 1024);
        entityType.IsActive = request.IsActive ?? entityType.IsActive;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("meta.entity_types.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entityType = await _db.EntityTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entityType == null)
            return NotFound(new { error = "not_found" });

        if (entityType.IsSystem)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "system_entity_type" });

        _db.EntityTypes.Remove(entityType);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeTitle(string value)
    {
        var s = value.Trim();
        return s.Length == 0 ? "Untitled" : s;
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    public sealed record UpsertMetaEntityTypeRequest
    {
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public bool? IsActive { get; init; }
    }
}
