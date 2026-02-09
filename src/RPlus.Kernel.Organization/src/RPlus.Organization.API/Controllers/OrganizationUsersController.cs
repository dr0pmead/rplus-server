using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Organization.Application.Interfaces;
using RPlus.SDK.Core.Attributes;
using System.Security.Claims;

namespace RPlus.Organization.Api.Controllers;

[ApiController]
[Route("api/organization/users")]
[Authorize]
public sealed class OrganizationUsersController : ControllerBase
{
    private readonly IOrganizationDbContext _db;

    public OrganizationUsersController(IOrganizationDbContext db)
    {
        _db = db;
    }

    // Used by Access for strong consistency checks (role snapshots).
    [HttpGet("{userId:guid}/assignments")]
    [Permission("organization.assignments.read", new[] { "WebAdmin" })]
    public async Task<IActionResult> GetAssignments(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var tenantId = ResolveTenantIdOrDefault();

        var now = DateTime.UtcNow;

        var assignments = await _db.UserAssignments.AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId
                && a.UserId == userId
                && !a.IsDeleted
                && a.ValidFrom <= now
                && (a.ValidTo == null || a.ValidTo > now))
            .Join(
                _db.OrgNodes.AsNoTracking().Where(n => n.TenantId == tenantId),
                a => a.NodeId,
                n => n.Id,
                (a, n) => new
                {
                    a.TenantId,
                    a.UserId,
                    NodeId = a.NodeId,
                    RoleCode = MapRoleCode(a.Role),
                    PathSnapshot = n.Path
                })
            .ToListAsync(ct);

        return Ok(assignments);
    }

    private static string MapRoleCode(string role) =>
        role.ToUpperInvariant() switch
        {
            "HEAD" => "org.head",
            "DEPUTY" => "org.deputy",
            _ => "org.employee"
        };

    private Guid ResolveTenantIdOrDefault()
    {
        var raw = User.FindFirstValue("tenant_id")
                  ?? User.FindFirstValue("tenantId")
                  ?? Request.Headers["x-tenant-id"].ToString();

        return Guid.TryParse(raw, out var tenantId) ? tenantId : Guid.Empty;
    }
}
