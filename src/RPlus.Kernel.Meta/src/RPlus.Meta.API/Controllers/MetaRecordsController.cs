using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System.Text.Json;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/records")]
[Authorize]
public sealed class MetaRecordsController : ControllerBase
{
    private readonly IMetaDbContext _db;

    public MetaRecordsController(IMetaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("meta.records.read")]
    public async Task<IActionResult> List([FromQuery] Guid? entityTypeId, [FromQuery] string? subjectType, [FromQuery] Guid? subjectId, CancellationToken ct)
    {
        var query = _db.Records.AsNoTracking();
        if (entityTypeId.HasValue && entityTypeId.Value != Guid.Empty)
            query = query.Where(x => x.EntityTypeId == entityTypeId.Value);

        if (!string.IsNullOrWhiteSpace(subjectType))
            query = query.Where(x => x.SubjectType == subjectType);

        if (subjectId.HasValue && subjectId.Value != Guid.Empty)
            query = query.Where(x => x.SubjectId == subjectId.Value);

        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.Id,
                x.EntityTypeId,
                x.SubjectType,
                x.SubjectId,
                x.OwnerUserId,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [RequiresPermission("meta.records.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var record = await _db.Records.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (record == null)
            return NotFound(new { error = "not_found" });

        return Ok(new
        {
            record.Id,
            record.EntityTypeId,
            record.SubjectType,
            record.SubjectId,
            record.OwnerUserId,
            record.CreatedAt,
            record.UpdatedAt
        });
    }

    [HttpPost]
    [RequiresPermission("meta.records.manage")]
    public async Task<IActionResult> Create([FromBody] CreateRecordRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        if (request.EntityTypeId == Guid.Empty)
            return BadRequest(new { error = "invalid_entity_type" });

        if (!await _db.EntityTypes.AnyAsync(x => x.Id == request.EntityTypeId, ct))
            return BadRequest(new { error = "entity_type_not_found" });

        var now = DateTime.UtcNow;
        var record = new MetaEntityRecord
        {
            Id = Guid.NewGuid(),
            EntityTypeId = request.EntityTypeId,
            SubjectType = NormalizeOptional(request.SubjectType, 128),
            SubjectId = request.SubjectId,
            OwnerUserId = request.OwnerUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Records.Add(record);
        await _db.SaveChangesAsync(ct);

        return Ok(new { record.Id });
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("meta.records.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRecordRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "invalid_request" });

        var record = await _db.Records.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (record == null)
            return NotFound(new { error = "not_found" });

        record.SubjectType = NormalizeOptional(request.SubjectType, 128) ?? record.SubjectType;
        record.SubjectId = request.SubjectId ?? record.SubjectId;
        record.OwnerUserId = request.OwnerUserId ?? record.OwnerUserId;
        record.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("meta.records.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var record = await _db.Records.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (record == null)
            return NotFound(new { error = "not_found" });

        var values = await _db.FieldValues.Where(x => x.RecordId == id).ToListAsync(ct);
        if (values.Count > 0)
            _db.FieldValues.RemoveRange(values);

        var relations = await _db.Relations.Where(x => x.FromRecordId == id || x.ToRecordId == id).ToListAsync(ct);
        if (relations.Count > 0)
            _db.Relations.RemoveRange(relations);

        _db.Records.Remove(record);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("{id:guid}/values")]
    [RequiresPermission("meta.records.read")]
    public async Task<IActionResult> GetValues(Guid id, CancellationToken ct)
    {
        var recordExists = await _db.Records.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!recordExists)
            return NotFound(new { error = "not_found" });

        var values = await _db.FieldValues.AsNoTracking()
            .Where(x => x.RecordId == id)
            .Select(x => new { x.FieldId, x.ValueJson, x.UpdatedAt })
            .ToListAsync(ct);

        return Ok(values);
    }

    [HttpPut("{id:guid}/values")]
    [RequiresPermission("meta.records.manage")]
    public async Task<IActionResult> SetValues(Guid id, [FromBody] UpsertValuesRequest request, CancellationToken ct)
    {
        if (request == null || request.Values.Count == 0)
            return BadRequest(new { error = "invalid_request" });

        var record = await _db.Records.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (record == null)
            return NotFound(new { error = "not_found" });

        var now = DateTime.UtcNow;
        foreach (var item in request.Values)
        {
            if (item.FieldId == Guid.Empty)
                continue;

            var json = NormalizeJson(item.ValueJson);
            if (json == null)
                return BadRequest(new { error = "invalid_json", fieldId = item.FieldId });

            var existing = await _db.FieldValues.FirstOrDefaultAsync(x => x.RecordId == id && x.FieldId == item.FieldId, ct);
            if (existing == null)
            {
                _db.FieldValues.Add(new MetaFieldValue
                {
                    Id = Guid.NewGuid(),
                    RecordId = id,
                    FieldId = item.FieldId,
                    ValueJson = json,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.ValueJson = json;
                existing.UpdatedAt = now;
            }
        }

        record.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    private static string? NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var _ = JsonDocument.Parse(json);
            return json.Trim();
        }
        catch
        {
            return null;
        }
    }

    public sealed record CreateRecordRequest
    {
        public Guid EntityTypeId { get; init; }
        public string? SubjectType { get; init; }
        public Guid? SubjectId { get; init; }
        public Guid? OwnerUserId { get; init; }
    }

    public sealed record UpdateRecordRequest
    {
        public string? SubjectType { get; init; }
        public Guid? SubjectId { get; init; }
        public Guid? OwnerUserId { get; init; }
    }

    public sealed record UpsertValuesRequest
    {
        public List<FieldValueItem> Values { get; init; } = new();
    }

    public sealed record FieldValueItem
    {
        public Guid FieldId { get; init; }
        public string? ValueJson { get; init; }
    }
}
