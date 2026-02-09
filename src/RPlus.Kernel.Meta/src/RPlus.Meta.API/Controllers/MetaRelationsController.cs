using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/relations")]
[Authorize]
public sealed class MetaRelationsController : ControllerBase
{
    private readonly IMetaDbContext _db;

    public MetaRelationsController(IMetaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("meta.relations.read")]
    public async Task<IActionResult> List([FromQuery] Guid? recordId, CancellationToken ct)
    {
        var query = _db.Relations.AsNoTracking();
        if (recordId.HasValue && recordId.Value != Guid.Empty)
            query = query.Where(x => x.FromRecordId == recordId.Value || x.ToRecordId == recordId.Value);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.FromRecordId,
                x.ToRecordId,
                x.RelationType,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [RequiresPermission("meta.relations.manage")]
    public async Task<IActionResult> Create([FromBody] CreateRelationRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        if (request.FromRecordId == Guid.Empty || request.ToRecordId == Guid.Empty)
            return BadRequest(new { error = "invalid_record" });

        var relationType = (request.RelationType ?? string.Empty).Trim();
        if (relationType.Length == 0)
            return BadRequest(new { error = "invalid_relation_type" });

        var exists = await _db.Relations.AnyAsync(x => x.FromRecordId == request.FromRecordId && x.ToRecordId == request.ToRecordId && x.RelationType == relationType, ct);
        if (exists)
            return Conflict(new { error = "relation_exists" });

        var relation = new MetaRelation
        {
            Id = Guid.NewGuid(),
            FromRecordId = request.FromRecordId,
            ToRecordId = request.ToRecordId,
            RelationType = relationType,
            CreatedAt = DateTime.UtcNow
        };

        _db.Relations.Add(relation);
        await _db.SaveChangesAsync(ct);
        return Ok(new { relation.Id });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("meta.relations.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var relation = await _db.Relations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (relation == null)
            return NotFound(new { error = "not_found" });

        _db.Relations.Remove(relation);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    public sealed record CreateRelationRequest
    {
        public Guid FromRecordId { get; init; }
        public Guid ToRecordId { get; init; }
        public string? RelationType { get; init; }
    }
}
