using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.HR.Api.Authorization;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Domain.Entities;
using RPlus.SDK.Access.Authorization;

namespace RPlus.HR.Api.Controllers;

[ApiController]
[Route("api/hr/fields")]
[Authorize]
public sealed class HrCustomFieldsController : ControllerBase
{
    private readonly IHrDbContext _db;

    public HrCustomFieldsController(IHrDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("hr.fields.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var fields = await _db.CustomFieldDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Group)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Label)
            .ToListAsync(ct);

        return Ok(fields.Select(Map));
    }

    [HttpPost]
    [RequiresPermission("hr.fields.manage")]
    public async Task<IActionResult> Create([FromBody] UpsertCustomFieldDefinitionRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Label))
            return BadRequest(new { error = "invalid_request" });

        var key = request.Key.Trim();
        var exists = await _db.CustomFieldDefinitions.AsNoTracking().AnyAsync(x => x.Key == key, ct);
        if (exists)
            return Conflict(new { error = "field_exists" });

        var now = DateTime.UtcNow;
        var field = new HrCustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = request.Label.Trim(),
            Type = request.Type?.Trim() ?? "text",
            Required = request.Required,
            Group = request.Group?.Trim() ?? "General",
            Order = request.Order,
            IsActive = request.IsActive ?? true,
            IsSystem = false,
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            Pattern = request.Pattern?.Trim(),
            Placeholder = request.Placeholder?.Trim(),
            OptionsJson = request.OptionsJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CustomFieldDefinitions.Add(field);
        await _db.SaveChangesAsync(ct);

        return Ok(Map(field));
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("hr.fields.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCustomFieldDefinitionRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty || request is null)
            return BadRequest(new { error = "invalid_request" });

        var field = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (field == null)
            return NotFound(new { error = "not_found" });
        if (field.IsSystem)
            return Forbid();

        field.Label = request.Label?.Trim() ?? field.Label;
        field.Type = request.Type?.Trim() ?? field.Type;
        field.Required = request.Required;
        field.Group = request.Group?.Trim() ?? field.Group;
        field.Order = request.Order;
        field.IsActive = request.IsActive ?? field.IsActive;
        field.MinLength = request.MinLength;
        field.MaxLength = request.MaxLength;
        field.Pattern = request.Pattern?.Trim();
        field.Placeholder = request.Placeholder?.Trim();
        field.OptionsJson = request.OptionsJson;
        field.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(field));
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("hr.fields.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "invalid_id" });

        var field = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (field == null)
            return NotFound(new { error = "not_found" });
        if (field.IsSystem)
            return Forbid();

        _db.CustomFieldDefinitions.Remove(field);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    private static CustomFieldDefinitionDto Map(HrCustomFieldDefinition field) => new(
        field.Id,
        field.Key,
        field.Label,
        field.Type,
        field.Required,
        field.Group,
        field.Order,
        field.IsActive,
        field.IsSystem,
        field.MinLength,
        field.MaxLength,
        field.Pattern,
        field.Placeholder,
        field.OptionsJson,
        field.CreatedAt,
        field.UpdatedAt);

    public sealed record CustomFieldDefinitionDto(
        Guid Id,
        string Key,
        string Label,
        string Type,
        bool Required,
        string Group,
        int Order,
        bool IsActive,
        bool IsSystem,
        int? MinLength,
        int? MaxLength,
        string? Pattern,
        string? Placeholder,
        string? OptionsJson,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public sealed record UpsertCustomFieldDefinitionRequest(
        string Key,
        string Label,
        string? Type,
        bool Required,
        string? Group,
        int Order,
        bool? IsActive,
        int? MinLength,
        int? MaxLength,
        string? Pattern,
        string? Placeholder,
        string? OptionsJson);
}
