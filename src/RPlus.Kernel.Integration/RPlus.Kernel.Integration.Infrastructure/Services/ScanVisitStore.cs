using RPlus.Kernel.Integration.Application.Services;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class ScanVisitStore : IScanVisitStore
{
    private const string KeyPrefix = "sys:integ:visit:";
    private readonly IConnectionMultiplexer _redis;

    public ScanVisitStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> ToggleVisitAsync(
        string contextId,
        string userId,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contextId) || string.IsNullOrWhiteSpace(userId))
            return true;

        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{contextId}:{userId}".ToLowerInvariant();

        if (await db.KeyExistsAsync(key))
        {
            await db.KeyDeleteAsync(key);
            return false;
        }

        await db.StringSetAsync(key, "1", ttl);
        return true;
    }
}
