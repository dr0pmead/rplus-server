using Microsoft.EntityFrameworkCore;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Access.Application.Services;

public class EffectiveRightsService : IEffectiveRightsService
{
    private readonly IAccessDbContext _dbContext;
    private readonly IRootAccessService _rootAccessService;

    public EffectiveRightsService(IAccessDbContext dbContext, IRootAccessService rootAccessService)
    {
        _dbContext = dbContext;
        _rootAccessService = rootAccessService;
    }

    public async Task<string> GetEffectivePermissionsJsonAsync(
        Guid userId,
        Guid tenantId,
        string? context,
        CancellationToken ct)
    {
        string contextKey = context ?? "global";

        // 1. Check existing snapshot
        var snapshot = await _dbContext.EffectiveSnapshots
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId && x.Context == contextKey, ct);

        if (snapshot != null && snapshot.ExpiresAt > DateTime.UtcNow)
        {
            return snapshot.DataJson;
        }

        var permissions = new Dictionary<string, bool>();

        // 2. Check if Root
        if (await _rootAccessService.IsRootAsync(userId.ToString(), ct))
        {
            // Root bypass: return a wildcard permission to avoid sending hundreds/thousands of rights.
            // Backend permission checks use RootRegistry (HMAC) and do not rely on enumerated permissions.
            permissions["*"] = true;
        }
        else
        {
            // 3. Get roles via UserAssignments
            var roleCodes = await _dbContext.UserAssignments
                .Where(x => x.UserId == userId && (x.TenantId == tenantId || x.TenantId == Guid.Empty))
                .Select(x => x.RoleCode)
                .Distinct()
                .ToListAsync(ct);

            if (roleCodes.Any())
            {
                var roleIds = await _dbContext.Roles
                    .Where(r => roleCodes.Contains(r.Code))
                    .Select(r => r.Id)
                    .ToListAsync(ct);

                if (roleIds.Any())
                {
                    // 4. Get policies for these roles
                    var rolePolicies = await (from ap in _dbContext.AccessPolicies
                                              join p in _dbContext.Permissions on ap.PermissionId equals p.Id
                                              where roleIds.Contains(ap.RoleId) && (ap.TenantId == tenantId || ap.TenantId == Guid.Empty)
                                              select new
                                              {
                                                  PermissionId = p.Id,
                                                  Effect = ap.Effect,
                                                  SupportedContexts = p.SupportedContexts
                                              }).ToListAsync(ct);

                    foreach (var policy in rolePolicies)
                    {
                        if (contextKey == "global" || policy.SupportedContexts == null || policy.SupportedContexts.Length == 0 || policy.SupportedContexts.Contains(contextKey))
                        {
                            bool isAllow = policy.Effect == "ALLOW";
                            if (permissions.TryGetValue(policy.PermissionId, out bool current))
                            {
                                if (!isAllow) permissions[policy.PermissionId] = false;
                            }
                            else
                            {
                                permissions[policy.PermissionId] = isAllow;
                            }
                        }
                    }
                }
            }

            // 5. Direct assignments
            var directAssignments = await _dbContext.PolicyAssignments
                .Where(x => x.TenantId == tenantId && x.TargetType == "User" && x.TargetId == userId.ToString())
                .ToListAsync(ct);

            if (directAssignments.Any())
            {
                var assignedPermIds = directAssignments.Select(da => da.PermissionId).ToList();
                var permContexts = await _dbContext.Permissions
                    .Where(p => assignedPermIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.SupportedContexts })
                    .ToDictionaryAsync(p => p.Id, p => p.SupportedContexts, ct);

                foreach (var pa in directAssignments)
                {
                    if (permContexts.TryGetValue(pa.PermissionId, out var contexts) &&
                        (contextKey == "global" || contexts == null || contexts.Length == 0 || contexts.Contains(contextKey)))
                    {
                        permissions[pa.PermissionId] = pa.Effect == "ALLOW";
                    }
                }
            }
        }

        // 6. Save or update snapshot
        string json = JsonSerializer.Serialize(permissions);
        if (snapshot == null)
        {
            snapshot = new EffectiveSnapshot
            {
                UserId = userId,
                TenantId = tenantId,
                Context = contextKey,
                DataJson = json,
                CalculatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Version = 1
            };
            _dbContext.EffectiveSnapshots.Add(snapshot);
        }
        else
        {
            snapshot.DataJson = json;
            snapshot.CalculatedAt = DateTime.UtcNow;
            snapshot.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
            snapshot.Version++;
        }

        await _dbContext.SaveChangesAsync(ct);
        return json;
    }

    public async Task InvalidateSnapshotAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        var existing = await _dbContext.EffectiveSnapshots
            .Where(x => x.UserId == userId && x.TenantId == tenantId)
            .ToListAsync(ct);

        if (existing.Any())
        {
            _dbContext.EffectiveSnapshots.RemoveRange(existing);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
