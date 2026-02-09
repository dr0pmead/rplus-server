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
[Route("api/access/policies")]
[Authorize]
public sealed class AccessPoliciesController : ControllerBase
{
    private readonly AccessDbContext _db;

    public AccessPoliciesController(AccessDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("access.policies.read")]
    public async Task<IActionResult> List([FromQuery] string? roleCode, [FromQuery] string? tenantId, CancellationToken ct)
    {
        Guid? tenant = null;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            if (!Guid.TryParse(tenantId, out var parsed))
                return BadRequest(new { error = "invalid_tenant_id" });
            tenant = parsed;
        }

        var query =
            from policy in _db.AccessPolicies.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on policy.RoleId equals role.Id
            select new
            {
                policy.Id,
                policy.TenantId,
                roleCode = role.Code,
                policy.PermissionId,
                policy.Effect,
                policy.ScopeType,
                policy.Priority,
                policy.ConditionExpression,
                policy.RequiredAuthLevel,
                policy.MaxAuthAgeSeconds,
                policy.CreatedAt
            };

        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            var rc = roleCode.Trim();
            query = query.Where(x => x.roleCode == rc);
        }

        if (tenant.HasValue)
            query = query.Where(x => x.TenantId == tenant.Value);

        var policies = await query
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.PermissionId)
            .ToListAsync(ct);

        return Ok(policies);
    }

    [HttpPost]
    [RequiresPermission("access.policies.create")]
    public async Task<IActionResult> Create([FromBody] CreatePolicyRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var roleCode = (request.RoleCode ?? string.Empty).Trim();
        var permissionId = (request.PermissionId ?? string.Empty).Trim();
        var effect = NormalizeEffect(request.Effect);
        var scopeType = string.IsNullOrWhiteSpace(request.ScopeType) ? "GLOBAL" : request.ScopeType.Trim().ToUpperInvariant();
        var priority = request.Priority;

        if (string.IsNullOrWhiteSpace(roleCode))
            return BadRequest(new { error = "invalid_role_code" });
        if (string.IsNullOrWhiteSpace(permissionId) || permissionId.Length > 150)
            return BadRequest(new { error = "invalid_permission_id" });
        if (effect is null)
            return BadRequest(new { error = "invalid_effect" });
        if (scopeType.Length > 20)
            return BadRequest(new { error = "invalid_scope_type" });

        var tenantGuid = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            if (!Guid.TryParse(request.TenantId, out tenantGuid))
                return BadRequest(new { error = "invalid_tenant_id" });
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode, ct);
        if (role == null)
            return NotFound(new { error = "role_not_found" });

        var permissionExists = await _db.Permissions.AnyAsync(p => p.Id == permissionId, ct);
        if (!permissionExists)
            return NotFound(new { error = "permission_not_found" });

        var exists = await _db.AccessPolicies.AnyAsync(
            p => p.TenantId == tenantGuid && p.RoleId == role.Id && p.PermissionId == permissionId,
            ct);
        if (exists)
            return Conflict(new { error = "policy_already_exists" });

        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantGuid,
            RoleId = role.Id,
            PermissionId = permissionId,
            Effect = effect,
            ScopeType = scopeType,
            Priority = priority,
            ConditionExpression = string.IsNullOrWhiteSpace(request.ConditionExpression) ? null : request.ConditionExpression,
            RequiredAuthLevel = request.RequiredAuthLevel,
            MaxAuthAgeSeconds = request.MaxAuthAgeSeconds,
            CreatedAt = DateTime.UtcNow
        };

        _db.AccessPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);

        await InvalidateAllEffectiveSnapshotsAsync(ct);

        return Created($"/api/access/policies/{policy.Id}", new { policy.Id });
    }

    [HttpGet("{id:guid}")]
    [RequiresPermission("access.policies.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var policy = await _db.AccessPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null)
            return NotFound(new { error = "policy_not_found" });

        var roleCode = await _db.Roles.AsNoTracking()
            .Where(r => r.Id == policy.RoleId)
            .Select(r => r.Code)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            policy.Id,
            policy.TenantId,
            RoleCode = roleCode,
            policy.PermissionId,
            policy.Effect,
            policy.ScopeType,
            policy.Priority,
            policy.ConditionExpression,
            policy.RequiredAuthLevel,
            policy.MaxAuthAgeSeconds,
            policy.CreatedAt
        });
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission("access.policies.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePolicyRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var policy = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null)
            return NotFound(new { error = "policy_not_found" });

        var effect = request.Effect == null ? null : NormalizeEffect(request.Effect);
        if (request.Effect != null && effect is null)
            return BadRequest(new { error = "invalid_effect" });

        if (request.ScopeType != null)
        {
            var scopeType = request.ScopeType.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(scopeType) || scopeType.Length > 20)
                return BadRequest(new { error = "invalid_scope_type" });
            policy.ScopeType = scopeType;
        }

        if (effect != null)
            policy.Effect = effect;

        if (request.Priority.HasValue)
            policy.Priority = request.Priority.Value;

        if (request.ConditionExpression != null)
            policy.ConditionExpression = string.IsNullOrWhiteSpace(request.ConditionExpression) ? null : request.ConditionExpression;

        if (request.RequiredAuthLevel.HasValue)
            policy.RequiredAuthLevel = request.RequiredAuthLevel;

        if (request.MaxAuthAgeSeconds.HasValue)
            policy.MaxAuthAgeSeconds = request.MaxAuthAgeSeconds;

        await _db.SaveChangesAsync(ct);
        await InvalidateAllEffectiveSnapshotsAsync(ct);

        return Ok(new { policy.Id });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission("access.policies.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var policy = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null)
            return NotFound(new { error = "policy_not_found" });

        _db.AccessPolicies.Remove(policy);
        await _db.SaveChangesAsync(ct);
        await InvalidateAllEffectiveSnapshotsAsync(ct);

        return Ok(new { success = true });
    }

    private static string? NormalizeEffect(string? effect)
    {
        if (string.IsNullOrWhiteSpace(effect))
            return null;

        var v = effect.Trim().ToUpperInvariant();
        return v is "ALLOW" or "DENY" ? v : null;
    }

    private async Task InvalidateAllEffectiveSnapshotsAsync(CancellationToken ct)
    {
        // We intentionally invalidate aggressively for correctness. Targeted invalidation can be added later.
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM access.effective_snapshots;", ct);
    }

    public sealed record CreatePolicyRequest(
        string RoleCode,
        string PermissionId,
        string? Effect,
        string? ScopeType,
        int Priority,
        string? TenantId,
        string? ConditionExpression,
        int? RequiredAuthLevel,
        int? MaxAuthAgeSeconds,
        string? ApplicationId,
        string[]? SupportedContexts);

    public sealed record UpdatePolicyRequest(
        string? Effect,
        string? ScopeType,
        int? Priority,
        string? ConditionExpression,
        int? RequiredAuthLevel,
        int? MaxAuthAgeSeconds);
}
