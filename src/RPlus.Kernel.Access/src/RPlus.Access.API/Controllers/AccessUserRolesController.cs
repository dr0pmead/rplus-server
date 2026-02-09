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
[Route("api/access/users/{userId:guid}/roles")]
[Authorize]
public sealed class AccessUserRolesController : ControllerBase
{
    private const string DefaultGlobalPath = "global";

    private readonly AccessDbContext _db;

    public AccessUserRolesController(AccessDbContext db) => _db = db;

    [HttpGet]
    [RequiresPermission("access.user_roles.read")]
    public async Task<IActionResult> List(Guid userId, CancellationToken ct)
    {
        var roles =
            from a in _db.UserAssignments.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on a.RoleCode equals r.Code into roleJoin
            from r in roleJoin.DefaultIfEmpty()
            where a.UserId == userId
            orderby a.RoleCode
            select new
            {
                a.TenantId,
                a.NodeId,
                a.RoleCode,
                roleName = r == null ? null : r.Name,
                a.PathSnapshot
            };

        return Ok(new { items = await roles.ToListAsync(ct) });
    }

    [HttpPost]
    [RequiresPermission("access.user_roles.manage")]
    public async Task<IActionResult> Add(Guid userId, [FromBody] AddRoleRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var roleCode = (request.RoleCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(roleCode))
            return BadRequest(new { error = "invalid_role_code" });

        var roleExists = await _db.Roles.AnyAsync(r => r.Code == roleCode, ct);
        if (!roleExists)
            return NotFound(new { error = "role_not_found" });

        var tenantId = request.TenantId ?? Guid.Empty;
        var nodeId = request.NodeId ?? Guid.Empty;
        var pathSnapshot = string.IsNullOrWhiteSpace(request.PathSnapshot) ? DefaultGlobalPath : request.PathSnapshot.Trim();

        var exists = await _db.UserAssignments.AnyAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.NodeId == nodeId && x.RoleCode == roleCode,
            ct);

        if (exists)
            return Ok(new { success = true });

        _db.UserAssignments.Add(new LocalUserAssignment
        {
            TenantId = tenantId,
            UserId = userId,
            NodeId = nodeId,
            RoleCode = roleCode,
            PathSnapshot = pathSnapshot
        });

        await _db.SaveChangesAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM access.effective_snapshots;", ct);

        return Ok(new { success = true });
    }

    [HttpDelete("{roleCode}")]
    [RequiresPermission("access.user_roles.manage")]
    public async Task<IActionResult> Remove(
        Guid userId,
        string roleCode,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? nodeId,
        CancellationToken ct)
    {
        var rc = (roleCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rc))
            return BadRequest(new { error = "invalid_role_code" });

        var resolvedTenantId = tenantId ?? Guid.Empty;
        var resolvedNodeId = nodeId ?? Guid.Empty;

        var entity = await _db.UserAssignments.FirstOrDefaultAsync(
            x => x.TenantId == resolvedTenantId && x.UserId == userId && x.NodeId == resolvedNodeId && x.RoleCode == rc,
            ct);

        if (entity is null)
            return NotFound(new { error = "assignment_not_found" });

        _db.UserAssignments.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM access.effective_snapshots;", ct);

        return Ok(new { success = true });
    }

    public sealed record AddRoleRequest
    {
        public string RoleCode { get; init; } = string.Empty;
        public Guid? TenantId { get; init; }
        public Guid? NodeId { get; init; }
        public string? PathSnapshot { get; init; }
    }
}

