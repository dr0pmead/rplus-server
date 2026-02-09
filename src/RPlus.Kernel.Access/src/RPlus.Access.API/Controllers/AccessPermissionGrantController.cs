using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Domain.Entities;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System;
using System.Linq;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/access/permissions")]
[Authorize]
public sealed class AccessPermissionGrantController : ControllerBase
{
    private readonly AccessDbContext _db;

    public AccessPermissionGrantController(AccessDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("access.roles.read")]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] Guid? appId, [FromQuery] bool includeDeprecated, CancellationToken ct)
    {
        var query = _db.Permissions.AsNoTracking();

        if (appId.HasValue && appId.Value != Guid.Empty)
            query = query.Where(p => p.AppId == appId.Value);

        if (!includeDeprecated)
            query = query.Where(p => p.Status == "ACTIVE");

        var term = (q ?? string.Empty).Trim();
        if (term.Length > 0)
        {
            term = term.ToLowerInvariant();
            query = query.Where(p =>
                (p.Id ?? string.Empty).ToLower().Contains(term) ||
                (p.Title ?? string.Empty).ToLower().Contains(term) ||
                (p.Resource ?? string.Empty).ToLower().Contains(term) ||
                (p.Action ?? string.Empty).ToLower().Contains(term));
        }

        var items = await query
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                id = p.Id,
                appId = p.AppId,
                resource = p.Resource,
                action = p.Action,
                title = p.Title,
                description = p.Description,
                status = p.Status,
                supportedContexts = p.SupportedContexts,
                sourceService = p.SourceService
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    [HttpPost("grant")]
    [RequiresPermission("access.policies.create")]
    public async Task<IActionResult> Grant([FromBody] GrantPermissionRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var roleCode = (request.RoleCode ?? string.Empty).Trim();
        var permissionId = (request.PermissionId ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(roleCode))
            return BadRequest(new { error = "invalid_role_code" });
        if (string.IsNullOrWhiteSpace(permissionId))
            return BadRequest(new { error = "invalid_permission_id" });

        var tenantId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(request.TenantId) && !Guid.TryParse(request.TenantId, out tenantId))
            return BadRequest(new { error = "invalid_tenant_id" });

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode, ct);
        if (role is null)
            return NotFound(new { error = "role_not_found" });

        var permissionExists = await _db.Permissions.AnyAsync(p => p.Id == permissionId, ct);
        if (!permissionExists)
            return NotFound(new { error = "permission_not_found" });

        var exists = await _db.AccessPolicies.AnyAsync(
            p => p.TenantId == tenantId && p.RoleId == role.Id && p.PermissionId == permissionId,
            ct);
        if (exists)
            return Conflict(new { error = "policy_already_exists" });

        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleId = role.Id,
            PermissionId = permissionId,
            Effect = "ALLOW",
            ScopeType = "GLOBAL",
            Priority = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.AccessPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);

        await _db.Database.ExecuteSqlRawAsync("DELETE FROM access.effective_snapshots;", ct);

        return Ok(new { policyId = policy.Id });
    }

    public sealed class GrantPermissionRequest
    {
        public string RoleCode { get; set; } = string.Empty;
        public string PermissionId { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }
}
