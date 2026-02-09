using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;

namespace RPlus.Meta.Api.Controllers;

[ApiController]
[Route("api/meta/overview")]
[Authorize]
public sealed class MetaOverviewController : ControllerBase
{
    private readonly IMetaDbContext _db;

    public MetaOverviewController(IMetaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("meta.read")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var types = await _db.EntityTypes.AsNoTracking()
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

        var fields = await _db.FieldDefinitions.AsNoTracking()
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

        var lists = await _db.Lists.AsNoTracking()
            .OrderBy(x => x.Title)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.Title,
                x.Description,
                x.SyncMode,
                x.IsSystem,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { entityTypes = types, fields, lists });
    }
}
