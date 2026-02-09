using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RPlus.Loyalty.Infrastructure.Services;

/// <summary>
/// Redis cache for wallet balances with gRPC fallback.
/// </summary>
public interface IWalletBalanceCache
{
    Task<decimal?> GetBalanceAsync(Guid userId, CancellationToken ct = default);
    Task SetBalanceAsync(Guid userId, decimal balance, CancellationToken ct = default);
}

public sealed class WalletBalanceCache : IWalletBalanceCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<WalletBalanceCache> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public WalletBalanceCache(IConnectionMultiplexer redis, ILogger<WalletBalanceCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<decimal?> GetBalanceAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(userId);
            var value = await db.StringGetAsync(key);
            
            if (value.IsNullOrEmpty)
                return null;
            
            if (decimal.TryParse(value.ToString(), out var balance))
                return balance;
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached balance for {UserId}", userId);
            return null;
        }
    }

    public async Task SetBalanceAsync(Guid userId, decimal balance, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(userId);
            await db.StringSetAsync(key, balance.ToString("F2"), CacheTtl);
            
            _logger.LogDebug("Cached balance {Balance} for {UserId}", balance, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache balance for {UserId}", userId);
        }
    }

    private static string GetKey(Guid userId) => $"loyalty:wallet:balance:{userId:D}";
}
