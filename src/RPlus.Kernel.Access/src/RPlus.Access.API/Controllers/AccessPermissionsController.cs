using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/access/permissions")]
[Authorize]
public sealed class AccessPermissionsController : ControllerBase
{
    private readonly AccessDbContext _db;

    public AccessPermissionsController(AccessDbContext db) => _db = db;

    [HttpGet]
    [RequiresPermission("access.permissions.read")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] bool includeDeprecated,
        CancellationToken ct)
    {
        var query = _db.Permissions.AsNoTracking();

        if (!includeDeprecated)
            query = query.Where(p => p.Status != "DEPRECATED");

        var term = (q ?? string.Empty).Trim();
        if (term.Length > 0)
        {
            var like = $"%{term}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Id, like) ||
                EF.Functions.ILike(p.Title, like) ||
                EF.Functions.ILike(p.Resource, like) ||
                EF.Functions.ILike(p.Action, like));
        }

        var items = await query
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Action)
            .ThenBy(p => p.Id)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                description = p.Description,
                resource = p.Resource,
                action = p.Action,
                status = p.Status,
                appId = p.AppId,
                supportedContexts = p.SupportedContexts,
                sourceService = p.SourceService,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }
}

