using RPlus.Kernel.Integration.Api.Services;
using RPlus.Kernel.Integration.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Api.Services;

/// <summary>
/// Interface for fallback profile aggregation when cache is empty.
/// </summary>
public interface IScanProfileAggregator
{
    /// <summary>
    /// Fetches profile from HR and Loyalty services, caches it, and returns.
    /// Used on cold start when Redis is empty.
    /// </summary>
    Task<ScanProfile?> FetchAndCacheAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Fallback aggregator that fetches from source services when cache miss occurs.
/// This is the "slow path" (~80ms) used only on first scan or cache invalidation.
/// </summary>
public sealed class ScanProfileAggregator : IScanProfileAggregator
{
    private readonly IHrProfileClient _hrClient;
    private readonly ILoyaltyProfileClient _loyaltyClient;
    private readonly IScanProfileCache _cache;

    public ScanProfileAggregator(
        IHrProfileClient hrClient,
        ILoyaltyProfileClient loyaltyClient,
        IScanProfileCache cache)
    {
        _hrClient = hrClient;
        _loyaltyClient = loyaltyClient;
        _cache = cache;
    }

    public async Task<ScanProfile?> FetchAndCacheAsync(Guid userId, CancellationToken ct = default)
    {
        // Parallel fetch from both services
        var hrTask = _hrClient.GetBasicAsync(userId, ct);
        var loyaltyTask = _loyaltyClient.GetProfileAsync(userId, ct);

        HrProfileDto? hr = null;
        LoyaltyProfileDto? loyalty = null;

        try
        {
            hr = await hrTask.ConfigureAwait(false);
        }
        catch
        {
            // HR unavailable - use defaults
        }

        try
        {
            loyalty = await loyaltyTask.ConfigureAwait(false);
        }
        catch
        {
            // Loyalty unavailable - use defaults
        }

        if (hr is null && loyalty is null)
            return null;

        var totalDiscount = loyalty?.TotalDiscount ?? 0;
        if (totalDiscount == 0 && loyalty is not null)
        {
            totalDiscount = loyalty.Discount + loyalty.MotivationDiscount;
        }

        // v3.0: Use real CurrentLevel and TotalLevels from Loyalty API
        var currentLevel = loyalty?.CurrentLevel > 0 ? loyalty.CurrentLevel : (loyalty?.LevelNumber ?? 1);
        var totalLevels = loyalty?.TotalLevels > 0 ? loyalty.TotalLevels : 1;
        var rplusDiscount = totalDiscount;

        var profile = new ScanProfile
        {
            FirstName = hr?.FirstName ?? "",
            LastName = hr?.LastName ?? "",
            MiddleName = hr?.MiddleName,
            AvatarUrl = hr?.AvatarUrl,
            CurrentLevel = currentLevel,
            TotalLevels = totalLevels,
            RPlusDiscount = rplusDiscount,
            // Deprecated fields for backwards compatibility
            Level = currentLevel,
            MotivationBonus = loyalty?.MotivationDiscount ?? 0,
            DiscountUser = totalDiscount,
            UpdatedAt = DateTime.UtcNow
        };

        // Cache for future requests
        await _cache.SetAsync(userId, profile, ct).ConfigureAwait(false);

        return profile;
    }
}
