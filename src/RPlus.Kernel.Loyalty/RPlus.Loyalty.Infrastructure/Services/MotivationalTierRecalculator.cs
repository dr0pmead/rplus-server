using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Eventing.Abstractions;
using RPlusGrpc.Wallet;

namespace RPlus.Loyalty.Infrastructure.Services;

/// <summary>
/// Recalculator for monthly motivational discount tiers.
/// Follows the same pattern as TenureLevelRecalculator.
/// </summary>
public sealed class MotivationalTierRecalculator : IMotivationalTierRecalculator
{
    private const string StateKey = "system.loyalty.motivational.tier";
    private readonly IDbContextFactory<LoyaltyDbContext> _dbFactory;
    private readonly IMotivationalTierCatalog _catalog;
    private readonly ILoyaltyLevelCatalog _levelCatalog;
    private readonly WalletService.WalletServiceClient _wallet;
    private readonly IEventPublisher _events;
    private readonly ILogger<MotivationalTierRecalculator> _logger;

    public MotivationalTierRecalculator(
        IDbContextFactory<LoyaltyDbContext> dbFactory,
        IMotivationalTierCatalog catalog,
        ILoyaltyLevelCatalog levelCatalog,
        WalletService.WalletServiceClient wallet,
        IEventPublisher events,
        ILogger<MotivationalTierRecalculator> logger)
    {
        _dbFactory = dbFactory;
        _catalog = catalog;
        _levelCatalog = levelCatalog;
        _wallet = wallet;
        _events = events;
        _logger = logger;
    }

    public async Task<MotivationalRecalcResult> RecalculateAsync(MotivationalRecalcRequest request, CancellationToken ct = default)
    {
        try
        {
            var tiers = await _catalog.GetTiersAsync(ct);
            var ordered = tiers
                .Where(t => !string.IsNullOrWhiteSpace(t.Key))
                .OrderBy(t => t.MinPoints)
                .ThenBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ordered.Count == 0)
            {
                return new MotivationalRecalcResult(false, 0, 0, null, false, "tiers_empty");
            }

            var baseTier = ResolveBaseTier(ordered);
            var tiersHash = ComputeTiersHash(ordered);

            // Determine which month to calculate
            var (year, month) = ResolveYearMonth(request);

            await using var db = _dbFactory.CreateDbContext();

            // Check state to skip if already run
            var stateKey = $"{StateKey}:{year}:{month}";
            var state = await db.TenureStates.FirstOrDefaultAsync(s => s.Key == stateKey, ct);
            if (state is null)
            {
                state = new LoyaltyTenureState { Key = stateKey };
                db.TenureStates.Add(state);
            }

            var today = DateTime.UtcNow.Date;
            var alreadyRunToday = state.LastRunAtUtc?.Date == today;
            var sameHash = string.Equals(state.LevelsHash, tiersHash, StringComparison.OrdinalIgnoreCase);

            if (!request.Force && alreadyRunToday && sameHash)
            {
                return new MotivationalRecalcResult(true, 0, 0, tiersHash, true, null);
            }

            var totalUsers = await db.ProgramProfiles.AsNoTracking().CountAsync(ct);
            var updatedUsers = 0;

            var maxParallel = Math.Clamp(request.MaxParallel, 1, 64);
            var batchSize = Math.Clamp(request.BatchSize, 10, 2000);

            for (var offset = 0; offset < totalUsers; offset += batchSize)
            {
                var userIds = await db.ProgramProfiles.AsNoTracking()
                    .OrderBy(p => p.UserId)
                    .Skip(offset)
                    .Take(batchSize)
                    .Select(p => p.UserId)
                    .ToListAsync(ct);

                if (userIds.Count == 0)
                    continue;

                var tierByUser = new ConcurrentDictionary<Guid, TierMatch>();

                await Parallel.ForEachAsync(userIds, new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallel,
                    CancellationToken = ct
                }, async (userId, token) =>
                {
                    var monthlyPoints = await GetMonthlyPointsAsync(userId, year, month, token);
                    var match = ResolveTier(ordered, monthlyPoints, baseTier);
                    tierByUser[userId] = match;
                });

                var profiles = await db.ProgramProfiles
                    .Where(p => userIds.Contains(p.UserId))
                    .ToListAsync(ct);

                foreach (var profile in profiles)
                {
                    if (!tierByUser.TryGetValue(profile.UserId, out var match))
                        continue;

                    // Compare and update if changed
                    if (profile.MotivationDiscount != match.Discount)
                    {
                        profile.MotivationDiscount = match.Discount;
                        profile.UpdatedAtUtc = DateTime.UtcNow;
                        updatedUsers++;
                    }
                }

                await db.SaveChangesAsync(ct);

                // Publish cache update events for changed profiles
                foreach (var profile in profiles)
                {
                    if (!tierByUser.TryGetValue(profile.UserId, out var match))
                        continue;

                    // Only publish if motivation discount actually changed
                    if (profile.MotivationDiscount == match.Discount)
                    {
                        var levelInfo = await GetLevelInfoAsync(profile.Level, ct);
                        var rplusDiscount = profile.Discount + profile.MotivationDiscount;
                        await _events.PublishAsync(new LoyaltyProfileUpdatedEvent
                        {
                            UserId = profile.UserId,
                            CurrentLevel = levelInfo.Index,
                            TotalLevels = levelInfo.TotalLevels,
                            RPlusDiscount = rplusDiscount,
                            // Deprecated v2 fields for backwards compatibility
                            Level = levelInfo.Index,
                            MotivationBonus = profile.MotivationDiscount,
                            TotalDiscount = rplusDiscount,
                            BaseDiscount = profile.Discount,
                            MotivationDiscount = profile.MotivationDiscount,
                            UpdatedAt = DateTimeOffset.UtcNow
                        }, LoyaltyEventTopics.ProfileUpdated, profile.UserId.ToString(), ct).ConfigureAwait(false);
                    }
                }
            }

            state.LevelsHash = tiersHash;
            state.LastRunAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Motivational tier recalculation completed for {Year}/{Month}: {Total} users, {Updated} updated.",
                year, month, totalUsers, updatedUsers);

            return new MotivationalRecalcResult(true, totalUsers, updatedUsers, tiersHash, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Motivational tier recalculation failed.");
            return new MotivationalRecalcResult(false, 0, 0, null, false, "recalc_failed");
        }
    }

    public async Task<SingleUserMotivationalResult> RecalculateUserAsync(Guid userId, int year, int month, CancellationToken ct = default)
    {
        try
        {
            var tiers = await _catalog.GetTiersAsync(ct);
            var ordered = tiers
                .Where(t => !string.IsNullOrWhiteSpace(t.Key))
                .OrderBy(t => t.MinPoints)
                .ThenBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ordered.Count == 0)
            {
                return new SingleUserMotivationalResult(false, null, 0, 0, false, "tiers_empty");
            }

            var baseTier = ResolveBaseTier(ordered);
            var monthlyPoints = await GetMonthlyPointsAsync(userId, year, month, ct);
            var match = ResolveTier(ordered, monthlyPoints, baseTier);

            await using var db = _dbFactory.CreateDbContext();
            var profile = await db.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

            if (profile == null)
            {
                return new SingleUserMotivationalResult(false, null, 0, monthlyPoints, false, "profile_not_found");
            }

            var updated = false;
            if (profile.MotivationDiscount != match.Discount)
            {
                profile.MotivationDiscount = match.Discount;
                profile.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                updated = true;
            }

            _logger.LogInformation(
                "Recalculated motivational tier for user {UserId}: points={Points}, tier={Tier}, discount={Discount}, updated={Updated}",
                userId, monthlyPoints, match.Key, match.Discount, updated);

            // Publish cache update event if motivation discount changed
            if (updated)
            {
                var levelInfo = await GetLevelInfoAsync(profile.Level, ct);
                var rplusDiscount = profile.Discount + profile.MotivationDiscount;
                await _events.PublishAsync(new LoyaltyProfileUpdatedEvent
                {
                    UserId = userId,
                    CurrentLevel = levelInfo.Index,
                    TotalLevels = levelInfo.TotalLevels,
                    RPlusDiscount = rplusDiscount,
                    // Deprecated v2 fields for backwards compatibility
                    Level = levelInfo.Index,
                    MotivationBonus = profile.MotivationDiscount,
                    TotalDiscount = rplusDiscount,
                    BaseDiscount = profile.Discount,
                    MotivationDiscount = profile.MotivationDiscount,
                    UpdatedAt = DateTimeOffset.UtcNow
                }, LoyaltyEventTopics.ProfileUpdated, userId.ToString(), ct).ConfigureAwait(false);
            }

            return new SingleUserMotivationalResult(true, match.Key, match.Discount, monthlyPoints, updated, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Single user motivational recalculation failed for {UserId}.", userId);
            return new SingleUserMotivationalResult(false, null, 0, 0, false, "recalc_failed");
        }
    }

    private async Task<long> GetMonthlyPointsAsync(Guid userId, int year, int month, CancellationToken ct)
    {
        try
        {
            var response = await _wallet.GetMonthlyPointsAsync(
                new GetMonthlyPointsRequest
                {
                    UserId = userId.ToString(),
                    Year = year,
                    Month = month
                },
                cancellationToken: ct);

            return response.Success ? response.TotalPoints : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get monthly points for user {UserId}.", userId);
            return 0;
        }
    }

    private static (int Year, int Month) ResolveYearMonth(MotivationalRecalcRequest request)
    {
        if (request.Year.HasValue && request.Month.HasValue)
        {
            return (request.Year.Value, request.Month.Value);
        }

        // Default: previous month (motivational discount is always for completed month)
        var now = DateTime.UtcNow;
        var previousMonth = now.AddMonths(-1);
        return (previousMonth.Year, previousMonth.Month);
    }

    private static TierMatch ResolveBaseTier(IReadOnlyList<MotivationalTierEntry> ordered)
    {
        // Look for explicit "none" or "base" tier
        var baseTier = ordered.FirstOrDefault(t =>
            string.Equals(t.Key, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Key, "base", StringComparison.OrdinalIgnoreCase));

        if (baseTier != null)
        {
            return new TierMatch(baseTier.Key.Trim(), baseTier.Discount);
        }

        // Otherwise use the tier with lowest MinPoints
        var min = ordered.OrderBy(t => t.MinPoints).First();
        return new TierMatch(min.Key.Trim(), min.Discount);
    }

    private static TierMatch ResolveTier(
        IReadOnlyList<MotivationalTierEntry> ordered,
        long monthlyPoints,
        TierMatch baseTier)
    {
        TierMatch selected = baseTier;
        foreach (var tier in ordered)
        {
            if (monthlyPoints >= tier.MinPoints)
            {
                selected = new TierMatch(tier.Key.Trim(), tier.Discount);
            }
        }

        return selected;
    }

    private static string ComputeTiersHash(IReadOnlyList<MotivationalTierEntry> ordered)
    {
        var builder = new StringBuilder();
        foreach (var tier in ordered)
        {
            builder.Append(tier.Key.Trim().ToLowerInvariant())
                .Append('|')
                .Append(tier.MinPoints)
                .Append('|')
                .Append(tier.Discount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                .Append(';');
        }

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private readonly record struct TierMatch(string Key, decimal Discount);

    /// <summary>
    /// Get level info (index and total) from level catalog for v3.0 scalable discount system.
    /// </summary>
    private async Task<LevelInfo> GetLevelInfoAsync(string? levelKey, CancellationToken ct)
    {
        var levels = await _levelCatalog.GetLevelsAsync(ct);
        var ordered = levels
            .Where(l => !string.IsNullOrWhiteSpace(l.Key))
            .OrderBy(l => l.Years)
            .ThenBy(l => l.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ordered.Count == 0)
            return new LevelInfo(1, 1);

        var totalLevels = ordered.Count;
        if (string.IsNullOrWhiteSpace(levelKey))
            return new LevelInfo(1, totalLevels);

        for (int i = 0; i < ordered.Count; i++)
        {
            if (string.Equals(ordered[i].Key, levelKey, StringComparison.OrdinalIgnoreCase))
            {
                return new LevelInfo(i + 1, totalLevels); // 1-based index
            }
        }

        return new LevelInfo(1, totalLevels);
    }

    private readonly record struct LevelInfo(int Index, int TotalLevels);
}
