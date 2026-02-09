using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using RPlusGrpc.Access;
using System.Text.Json;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IAccessIntegrationPermissionService
{
    Task<bool> HasPermissionAsync(Guid apiKeyId, string permissionId, CancellationToken cancellationToken);
}

public sealed class AccessIntegrationPermissionService : IAccessIntegrationPermissionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly AccessService.AccessServiceClient _accessClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AccessIntegrationPermissionService> _logger;
    public AccessIntegrationPermissionService(
        AccessService.AccessServiceClient accessClient,
        IDistributedCache cache,
        ILogger<AccessIntegrationPermissionService> logger)
    {
        _accessClient = accessClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(Guid apiKeyId, string permissionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permissionId))
            return true;

        var cacheKey = GetCacheKey(apiKeyId);
        var cachedRaw = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedRaw))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<HashSet<string>>(cachedRaw) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return cached.Contains(permissionId);
            }
            catch
            {
                // ignore cache errors
            }
        }

        try
        {
            var response = await _accessClient.GetIntegrationPermissionsAsync(
                new GetIntegrationPermissionsRequest { ApiKeyId = apiKeyId.ToString() },
                cancellationToken: cancellationToken);

            var set = new HashSet<string>(response.Permissions, StringComparer.OrdinalIgnoreCase);
            var payload = JsonSerializer.Serialize(set);
            await _cache.SetStringAsync(
                cacheKey,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                cancellationToken);
            return set.Contains(permissionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch integration permissions for ApiKeyId={ApiKeyId}", apiKeyId);
            return false;
        }
    }

    private static string GetCacheKey(Guid apiKeyId) => $"integration:perms:{apiKeyId}";
}
