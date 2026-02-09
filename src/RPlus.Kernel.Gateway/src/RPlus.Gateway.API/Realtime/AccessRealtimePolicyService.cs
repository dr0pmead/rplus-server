using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.SDK.Gateway.Realtime;
using RPlusGrpc.Access;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed class AccessRealtimePolicyService : IRealtimePolicyService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);
    private readonly AccessService.AccessServiceClient _access;
    private readonly IOptionsMonitor<RealtimeGatewayOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AccessRealtimePolicyService> _logger;

    public AccessRealtimePolicyService(
        AccessService.AccessServiceClient access,
        IOptionsMonitor<RealtimeGatewayOptions> options,
        IMemoryCache cache,
        ILogger<AccessRealtimePolicyService> logger)
    {
        _access = access;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlySet<string>> GetGrantedPermissionsAsync(string userId, CancellationToken ct)
    {
        if (_cache.TryGetValue<HashSet<string>>(PermissionsCacheKey(userId), out var cached) && cached != null)
            return cached;

        HashSet<string> granted = new(StringComparer.Ordinal);

        try
        {
            var response = await _access.GetEffectiveRightsAsync(new GetEffectiveRightsRequest
            {
                UserId = userId,
                TenantId = Guid.Empty.ToString()
            }, cancellationToken: ct);

            if (!string.IsNullOrWhiteSpace(response.PermissionsJson))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(response.PermissionsJson);
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        if (kv.Value)
                            granted.Add(kv.Key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch effective rights for realtime registry (userId={UserId})", userId);
        }

        _cache.Set(PermissionsCacheKey(userId), granted, CacheTtl);
        return granted;
    }

    public async Task<IReadOnlyCollection<RealtimeEventDescriptor>> GetRegistryAsync(string userId, CancellationToken ct)
    {
        if (_cache.TryGetValue<IReadOnlyCollection<RealtimeEventDescriptor>>(RegistryCacheKey(userId), out var cached) && cached != null)
            return cached;

        var granted = await GetGrantedPermissionsAsync(userId, ct);
        var mappings = _options.CurrentValue.Mappings;
        var registry = RealtimeRegistryBuilder.BuildRegistry(mappings, granted);

        _cache.Set(RegistryCacheKey(userId), registry, CacheTtl);
        return registry;
    }

    public async Task<bool> IsAllowedAsync(string userId, string? requiredPermission, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission))
            return true;

        var granted = await GetGrantedPermissionsAsync(userId, ct);
        return granted.Contains(requiredPermission);
    }

    private static string PermissionsCacheKey(string userId) => $"realtime:perms:{userId}";
    private static string RegistryCacheKey(string userId) => $"realtime:registry:{userId}";
}

