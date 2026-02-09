using RPlus.Kernel.Integration.Application.Services;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Api.Services;

public sealed class RedisQrTokenStore : IQrTokenStore
{
    private const string ActiveKeyPrefix = "users:qr:active:";
    private const string TokenKeyPrefix = "users:qr:token:";

    private readonly IConnectionMultiplexer _redis;

    public RedisQrTokenStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Guid? TryGetUserId(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var db = _redis.GetDatabase();
        var value = db.StringGet(TokenKeyPrefix + token.Trim());
        if (value.IsNullOrEmpty)
            return null;

        return Guid.TryParse(value.ToString(), out var userId) ? userId : null;
    }

    public async Task InvalidateAsync(string token, string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userId))
            return;

        var db = _redis.GetDatabase();
        var tokenKey = TokenKeyPrefix + token.Trim();
        await db.KeyDeleteAsync(tokenKey);

        var activeKey = ActiveKeyPrefix + userId.Trim();
        var activeToken = await db.StringGetAsync(activeKey);
        if (!activeToken.IsNullOrEmpty && string.Equals(activeToken.ToString(), token, StringComparison.Ordinal))
        {
            await db.KeyDeleteAsync(activeKey);
        }
    }
}
