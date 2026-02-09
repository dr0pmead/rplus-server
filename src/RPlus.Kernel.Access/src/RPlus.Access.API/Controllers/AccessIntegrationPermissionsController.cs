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
[Route("api/access/integration-permissions")]
[Authorize]
public sealed class AccessIntegrationPermissionsController : ControllerBase
{
    private readonly AccessDbContext _db;

    public AccessIntegrationPermissionsController(AccessDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequiresPermission("access.permissions.read")]
    public async Task<IActionResult> List([FromQuery] string apiKeyId, CancellationToken ct)
    {
        if (!Guid.TryParse(apiKeyId, out var keyId))
            return BadRequest(new { error = "invalid_api_key_id" });

        var items = await _db.IntegrationApiKeyPermissions
            .AsNoTracking()
            .Where(p => p.ApiKeyId == keyId)
            .Join(_db.Permissions.AsNoTracking(),
                link => link.PermissionId,
                perm => perm.Id,
                (link, perm) => new
                {
                    id = perm.Id,
                    title = perm.Title,
                    description = perm.Description,
                    resource = perm.Resource,
                    action = perm.Action,
                    status = perm.Status,
                    appId = perm.AppId,
                    supportedContexts = perm.SupportedContexts,
                    sourceService = perm.SourceService,
                    grantedAt = link.GrantedAt
                })
            .OrderBy(p => p.resource)
            .ThenBy(p => p.action)
            .ThenBy(p => p.id)
            .ToListAsync(ct);

        return Ok(new { items });
    }

    [HttpPost("grant")]
    [RequiresPermission("access.policies.create")]
    public async Task<IActionResult> Grant([FromBody] GrantRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!Guid.TryParse(request.ApiKeyId, out var keyId))
            return BadRequest(new { error = "invalid_api_key_id" });

        var permissionId = (request.PermissionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(permissionId))
            return BadRequest(new { error = "invalid_permission_id" });

        var permissionExists = await _db.Permissions.AnyAsync(p => p.Id == permissionId, ct);
        if (!permissionExists)
            return NotFound(new { error = "permission_not_found" });

        var exists = await _db.IntegrationApiKeyPermissions
            .AnyAsync(p => p.ApiKeyId == keyId && p.PermissionId == permissionId, ct);
        if (exists)
            return Conflict(new { error = "permission_already_granted" });

        _db.IntegrationApiKeyPermissions.Add(new IntegrationApiKeyPermission
        {
            Id = Guid.NewGuid(),
            ApiKeyId = keyId,
            PermissionId = permissionId,
            GrantedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("revoke")]
    [RequiresPermission("access.policies.delete")]
    public async Task<IActionResult> Revoke([FromBody] RevokeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!Guid.TryParse(request.ApiKeyId, out var keyId))
            return BadRequest(new { error = "invalid_api_key_id" });

        var permissionId = (request.PermissionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(permissionId))
            return BadRequest(new { error = "invalid_permission_id" });

        var entity = await _db.IntegrationApiKeyPermissions
            .FirstOrDefaultAsync(p => p.ApiKeyId == keyId && p.PermissionId == permissionId, ct);
        if (entity is null)
            return NotFound(new { error = "permission_not_granted" });

        _db.IntegrationApiKeyPermissions.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    public sealed class GrantRequest
    {
        public string ApiKeyId { get; set; } = string.Empty;
        public string PermissionId { get; set; } = string.Empty;
    }

    public sealed class RevokeRequest
    {
        public string ApiKeyId { get; set; } = string.Empty;
        public string PermissionId { get; set; } = string.Empty;
    }
}
