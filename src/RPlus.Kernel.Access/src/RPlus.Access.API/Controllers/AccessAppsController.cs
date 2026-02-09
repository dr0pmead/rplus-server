using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Domain.Entities;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System.Text.RegularExpressions;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/access/apps")]
[Authorize]
public sealed class AccessAppsController : ControllerBase
{
    private static readonly Regex AppCodeRegex = new("^[a-zA-Z0-9][a-zA-Z0-9_-]{1,49}$", RegexOptions.Compiled);

    private readonly AccessDbContext _db;

    public AccessAppsController(AccessDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("access.apps.read")]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.Apps.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Code.Contains(term) || (a.Name != null && a.Name.Contains(term)));
        }

        var apps = await query
            .OrderBy(a => a.Code)
            .Select(a => new { a.Id, a.Code, a.Name })
            .ToListAsync(ct);

        return Ok(apps);
    }

    [HttpGet("{code}")]
    [RequiresPermission("access.apps.read")]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_app_code" });

        var app = await _db.Apps.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code, ct);
        return app == null
            ? NotFound(new { error = "app_not_found" })
            : Ok(new { app.Id, app.Code, app.Name });
    }

    [HttpPost]
    [RequiresPermission("access.apps.create")]
    public async Task<IActionResult> Create([FromBody] CreateAppRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();

        if (!AppCodeRegex.IsMatch(code))
            return BadRequest(new { error = "invalid_app_code" });
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return BadRequest(new { error = "invalid_app_name" });

        var exists = await _db.Apps.AnyAsync(a => a.Code == code, ct);
        if (exists)
            return Conflict(new { error = "app_already_exists" });

        var app = new App
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name
        };

        _db.Apps.Add(app);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/access/apps/{app.Code}", new { app.Id, app.Code, app.Name });
    }

    public sealed record CreateAppRequest(string Code, string Name);
}

