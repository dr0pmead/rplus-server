using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Domain.Entities;
using RPlus.SDK.Access.Authorization;

namespace RPlus.HR.Api.Controllers;

[ApiController]
[Route("api/hr/search")]
[Authorize]
public sealed class HrSearchController : ControllerBase
{
    private readonly IHrDbContext _db;

    public HrSearchController(IHrDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [RequiresPermission("hr.profile.view")]
    public async Task<IActionResult> Search([FromBody] HrSearchRequest request, CancellationToken ct)
    {
        request ??= new HrSearchRequest();

        var query = _db.EmployeeProfiles.AsNoTracking();

        if (request.HasDisability.HasValue)
        {
            query = request.HasDisability.Value
                ? query.Where(x => x.DisabilityGroup != DisabilityGroup.None)
                : query.Where(x => x.DisabilityGroup == DisabilityGroup.None);
        }

        if (!string.IsNullOrWhiteSpace(request.ClothingSize))
        {
            var size = request.ClothingSize.Trim();
            query = query.Where(x => x.ClothingSize != null && x.ClothingSize == size);
        }

        if (request.ChildAgeMax.HasValue)
        {
            var max = request.ChildAgeMax.Value;
            if (max < 0 || max > 120)
                return BadRequest(new { error = "invalid_child_age_max" });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var minBirthDate = today.AddYears(-max);

            query = query.Where(x =>
                x.FamilyMembers.Any(m =>
                    m.Relation == FamilyRelation.Child
                    && m.BirthDate.HasValue
                    && m.BirthDate.Value >= minBirthDate));
        }

        var limit = request.Limit is > 0 and <= 500 ? request.Limit.Value : 100;

        var result = await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Take(limit)
            .Select(p => new HrSearchItem
            {
                UserId = p.UserId,
                Iin = p.Iin,
                FirstName = p.FirstName,
                LastName = p.LastName,
                MiddleName = p.MiddleName,
                Status = p.Status.ToString(),
                DisabilityGroup = p.DisabilityGroup.ToString(),
                ClothingSize = p.ClothingSize
            })
            .ToListAsync(ct);

        return Ok(new { items = result });
    }

    public sealed record HrSearchRequest
    {
        public bool? HasDisability { get; init; }
        public string? ClothingSize { get; init; }
        public int? ChildAgeMax { get; init; }
        public int? Limit { get; init; }
    }

    public sealed record HrSearchItem
    {
        public Guid UserId { get; init; }
        public string? Iin { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string? MiddleName { get; init; }
        public string Status { get; init; } = string.Empty;
        public string DisabilityGroup { get; init; } = string.Empty;
        public string? ClothingSize { get; init; }
    }
}

