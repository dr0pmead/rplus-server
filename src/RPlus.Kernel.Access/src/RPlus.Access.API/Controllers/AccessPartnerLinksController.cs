using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Domain.Entities;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/access/partner-links")]
[Authorize]
public sealed class AccessPartnerLinksController : ControllerBase
{
    private readonly AccessDbContext _db;

    public AccessPartnerLinksController(AccessDbContext db) => _db = db;

    [HttpGet]
    [RequiresPermission("access.partner_links.read")]
    public async Task<IActionResult> List([FromQuery] Guid? applicationId, [FromQuery] Guid? userId, CancellationToken ct)
    {
        var query =
            from link in _db.PartnerUserLinks.AsNoTracking()
            join app in _db.Apps.AsNoTracking() on link.ApplicationId equals app.Id
            select new
            {
                link.ApplicationId,
                appCode = app.Code,
                appName = app.Name,
                link.UserId,
                link.CreatedAt
            };

        if (applicationId.HasValue && applicationId.Value != Guid.Empty)
            query = query.Where(x => x.ApplicationId == applicationId.Value);

        if (userId.HasValue && userId.Value != Guid.Empty)
            query = query.Where(x => x.UserId == userId.Value);

        var items = await query
            .OrderBy(x => x.appCode)
            .ThenBy(x => x.UserId)
            .ToListAsync(ct);

        return Ok(new { items });
    }

    [HttpPost]
    [RequiresPermission("access.partner_links.manage")]
    public async Task<IActionResult> Link([FromBody] LinkRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (request.ApplicationId == Guid.Empty)
            return BadRequest(new { error = "invalid_application_id" });
        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var appExists = await _db.Apps.AnyAsync(a => a.Id == request.ApplicationId, ct);
        if (!appExists)
            return NotFound(new { error = "app_not_found" });

        var exists = await _db.PartnerUserLinks.AnyAsync(
            x => x.ApplicationId == request.ApplicationId && x.UserId == request.UserId,
            ct);
        if (exists)
            return Ok(new { success = true });

        _db.PartnerUserLinks.Add(new PartnerUserLink
        {
            ApplicationId = request.ApplicationId,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete]
    [RequiresPermission("access.partner_links.manage")]
    public async Task<IActionResult> Unlink([FromQuery] Guid applicationId, [FromQuery] Guid userId, CancellationToken ct)
    {
        if (applicationId == Guid.Empty)
            return BadRequest(new { error = "invalid_application_id" });
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var entity = await _db.PartnerUserLinks.FirstOrDefaultAsync(
            x => x.ApplicationId == applicationId && x.UserId == userId,
            ct);

        if (entity is null)
            return NotFound(new { error = "not_found" });

        _db.PartnerUserLinks.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("me")]
    [RequiresPermission("access.partner.self.read")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdRaw = User.FindFirst("sub")?.Value
                        ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userIdRaw) || !Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized();

        var query =
            from link in _db.PartnerUserLinks.AsNoTracking()
            join app in _db.Apps.AsNoTracking() on link.ApplicationId equals app.Id
            where link.UserId == userId
            select new
            {
                link.ApplicationId,
                appCode = app.Code,
                appName = app.Name,
                linkedAt = link.CreatedAt
            };

        var items = await query.OrderBy(x => x.appCode).ToListAsync(ct);

        return Ok(new { items });
    }

    [HttpGet("me/keys")]
    [RequiresPermission("access.partner.self.read")]
    public async Task<IActionResult> MyKeys(CancellationToken ct)
    {
        var userIdRaw = User.FindFirst("sub")?.Value
                        ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userIdRaw) || !Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized();

        var appIds = await _db.PartnerUserLinks.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ApplicationId)
            .Distinct()
            .ToListAsync(ct);

        if (appIds.Count == 0)
            return Ok(new { items = Array.Empty<object>() });

        var items = await _db.IntegrationApiKeys.AsNoTracking()
            .Where(k => appIds.Contains(k.ApplicationId))
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new
            {
                k.Id,
                k.ApplicationId,
                k.Name,
                k.Environment,
                k.Status,
                k.CreatedAt,
                k.ExpiresAt,
                k.RevokedAt
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    public sealed record LinkRequest(Guid ApplicationId, Guid UserId);
}
