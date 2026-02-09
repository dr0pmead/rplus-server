using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Infrastructure.Options;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Eventing.Abstractions;
using RPlusGrpc.Hr;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class TenureLevelRecalculator : ITenureLevelRecalculator
{
    private const string StateKey = "system.loyalty.tenure.level";
    private readonly IDbContextFactory<LoyaltyDbContext> _dbFactory;
    private readonly ILoyaltyLevelCatalog _catalog;
    private readonly HrService.HrServiceClient _hr;
    private readonly IOptionsMonitor<LoyaltyUserContextOptions> _options;
    private readonly IEventPublisher _events;
    private readonly ILogger<TenureLevelRecalculator> _logger;

    public TenureLevelRecalculator(
        IDbContextFactory<LoyaltyDbContext> dbFactory,
        ILoyaltyLevelCatalog catalog,
        HrService.HrServiceClient hr,
        IOptionsMonitor<LoyaltyUserContextOptions> options,
        IEventPublisher events,
        ILogger<TenureLevelRecalculator> logger)
    {
        _dbFactory = dbFactory;
        _catalog = catalog;
        _hr = hr;
        _options = options;
        _events = events;
        _logger = logger;
    }

    public async Task<TenureRecalcResult> RecalculateAsync(TenureRecalcRequest request, CancellationToken ct = default)
    {
        try
        {
            var levels = await _catalog.GetLevelsAsync(ct);
            var ordered = levels
                .Where(l => !string.IsNullOrWhiteSpace(l.Key))
                .OrderBy(l => l.Years)
                .ThenBy(l => l.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ordered.Count == 0)
            {
                return new TenureRecalcResult(false, 0, 0, null, false, "levels_empty");
            }

            var baseLevel = ResolveBaseLevel(ordered);
            var levelsHash = ComputeLevelsHash(ordered);

            await using var db = _dbFactory.CreateDbContext();
            var state = await db.TenureStates.FirstOrDefaultAsync(s => s.Key == StateKey, ct);
            if (state is null)
            {
                state = new LoyaltyTenureState { Key = StateKey };
                db.TenureStates.Add(state);
            }

            var today = DateTime.UtcNow.Date;
            var alreadyRunToday = state.LastRunAtUtc?.Date == today;
            var sameHash = string.Equals(state.LevelsHash, levelsHash, StringComparison.OrdinalIgnoreCase);

            if (!request.Force && alreadyRunToday && sameHash)
            {
                return new TenureRecalcResult(true, 0, 0, levelsHash, true, null);
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
                {
                    continue;
                }

                var levelByUser = new ConcurrentDictionary<Guid, LevelMatch?>();

                await Parallel.ForEachAsync(userIds, new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallel,
                    CancellationToken = ct
                }, async (userId, token) =>
                {
                    var tenureYears = await GetTenureYearsAsync(userId, token);
                    // Skip if HR returned error (-1) - don't overwrite existing data
                    if (tenureYears < 0)
                    {
                        levelByUser[userId] = null; // Mark as failed
                        return;
                    }
                    var match = ResolveLevel(ordered, tenureYears, baseLevel);
                    levelByUser[userId] = match;
                });

                var profiles = await db.ProgramProfiles
                    .Where(p => userIds.Contains(p.UserId))
                    .ToListAsync(ct);

                foreach (var profile in profiles)
                {
                    if (!levelByUser.TryGetValue(profile.UserId, out var match) || match == null)
                        continue; // Skip if HR lookup failed - preserve existing data

                    var m = match.Value;
                    if (!string.Equals(profile.Level ?? string.Empty, m.Key, StringComparison.OrdinalIgnoreCase)
                        || profile.Discount != m.Discount)
                    {
                        profile.Level = m.Key;
                        profile.Discount = m.Discount;
                        profile.UpdatedAtUtc = DateTime.UtcNow;
                        updatedUsers++;
                    }
                }

                await db.SaveChangesAsync(ct);

                // Publish cache update events for changed profiles
                foreach (var profile in profiles)
                {
                    if (!levelByUser.TryGetValue(profile.UserId, out var match) || match == null)
                        continue;

                    var m = match.Value;
                    // Only publish if we actually updated this profile
                    if (string.Equals(profile.Level ?? string.Empty, m.Key, StringComparison.OrdinalIgnoreCase)
                        && profile.Discount == m.Discount)
                    {
                        // v3.0: Calculate RPlusDiscount = BaseDiscount + MotivationBonus
                        var rplusDiscount = profile.Discount + profile.MotivationDiscount;
                        await _events.PublishAsync(new LoyaltyProfileUpdatedEvent
                        {
                            UserId = profile.UserId,
                            CurrentLevel = m.Index,
                            TotalLevels = m.TotalLevels,
                            RPlusDiscount = rplusDiscount,
                            // Deprecated v2 fields for backwards compatibility
                            Level = m.Index,
                            MotivationBonus = profile.MotivationDiscount,
                            TotalDiscount = rplusDiscount,
                            BaseDiscount = profile.Discount,
                            MotivationDiscount = profile.MotivationDiscount,
                            UpdatedAt = DateTimeOffset.UtcNow
                        }, LoyaltyEventTopics.ProfileUpdated, profile.UserId.ToString(), ct).ConfigureAwait(false);
                    }
                }
            }

            state.LevelsHash = levelsHash;
            state.LastRunAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return new TenureRecalcResult(true, totalUsers, updatedUsers, levelsHash, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenure level recalculation failed.");
            return new TenureRecalcResult(false, 0, 0, null, false, "recalc_failed");
        }
    }

    public async Task<SingleUserRecalcResult> RecalculateUserAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var levels = await _catalog.GetLevelsAsync(ct);
            var ordered = levels
                .Where(l => !string.IsNullOrWhiteSpace(l.Key))
                .OrderBy(l => l.Years)
                .ThenBy(l => l.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ordered.Count == 0)
            {
                return new SingleUserRecalcResult(false, null, 0, false, "levels_empty");
            }

            var baseLevel = ResolveBaseLevel(ordered);
            var tenureYears = await GetTenureYearsAsync(userId, ct);
            
            // If HR service is unavailable, don't touch existing data
            if (tenureYears < 0)
            {
                _logger.LogWarning("Skipping tenure recalc for {UserId} - HR service unavailable", userId);
                return new SingleUserRecalcResult(false, null, 0, false, "hr_unavailable");
            }
            
            var match = ResolveLevel(ordered, tenureYears, baseLevel);

            await using var db = _dbFactory.CreateDbContext();
            var profile = await db.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

            if (profile == null)
            {
                return new SingleUserRecalcResult(false, null, 0, false, "profile_not_found");
            }

            var oldLevel = profile.Level ?? string.Empty;
            var oldDiscount = profile.Discount;
            var updated = false;

            if (!string.Equals(oldLevel, match.Key, StringComparison.OrdinalIgnoreCase) || oldDiscount != match.Discount)
            {
                profile.Level = match.Key;
                profile.Discount = match.Discount;
                profile.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                updated = true;
            }

            _logger.LogInformation("Recalculated tenure for user {UserId}: years={Years}, level={Level}, discount={Discount}, updated={Updated}",
                userId, tenureYears, match.Key, match.Discount, updated);

            // Publish cache update event if discount changed
            if (updated)
            {
                // v3.0: Calculate RPlusDiscount = BaseDiscount + MotivationBonus
                var rplusDiscount = profile.Discount + profile.MotivationDiscount;
                await _events.PublishAsync(new LoyaltyProfileUpdatedEvent
                {
                    UserId = userId,
                    CurrentLevel = match.Index,
                    TotalLevels = match.TotalLevels,
                    RPlusDiscount = rplusDiscount,
                    // Deprecated v2 fields for backwards compatibility
                    Level = match.Index,
                    MotivationBonus = profile.MotivationDiscount,
                    TotalDiscount = rplusDiscount,
                    BaseDiscount = profile.Discount,
                    MotivationDiscount = profile.MotivationDiscount,
                    UpdatedAt = DateTimeOffset.UtcNow
                }, LoyaltyEventTopics.ProfileUpdated, userId.ToString(), ct).ConfigureAwait(false);
            }

            return new SingleUserRecalcResult(true, match.Key, match.Discount, updated, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Single user tenure recalculation failed for {UserId}.", userId);
            return new SingleUserRecalcResult(false, null, 0, false, "recalc_failed");
        }
    }

    private async Task<int> GetTenureYearsAsync(Guid userId, CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        Metadata? headers = null;
        var secret = (opts.HrSharedSecret ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(secret))
        {
            headers = new Metadata { { "x-rplus-service-secret", secret } };
        }

        try
        {
            var response = await _hr.GetVitalStatsAsync(new GetVitalStatsRequest { UserId = userId.ToString() }, headers: headers, cancellationToken: ct);
            return ComputeTenureYearsFromDays(response.TenureDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenure for user {UserId}, HR may be unavailable", userId);
            return -1; // Return -1 to indicate error, not 0 (which would reset tenure)
        }
    }

    private static int ComputeTenureYearsFromDays(int days)
    {
        if (days <= 0)
            return 0;

        var years = days / 365.2425d;
        return (int)Math.Floor(years);
    }

    private static LevelMatch ResolveBaseLevel(IReadOnlyList<LoyaltyLevelEntry> ordered)
    {
        var totalLevels = ordered.Count;
        
        // Find base level by key
        for (int i = 0; i < ordered.Count; i++)
        {
            if (string.Equals(ordered[i].Key, "base", StringComparison.OrdinalIgnoreCase))
            {
                return new LevelMatch(ordered[i].Key.Trim(), ordered[i].Discount, i + 1, totalLevels);
            }
        }

        // Fallback to first level (min years)
        var minYears = ordered.Min(l => l.Years);
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Years == minYears)
            {
                return new LevelMatch(ordered[i].Key.Trim(), ordered[i].Discount, i + 1, totalLevels);
            }
        }

        // Ultimate fallback
        return new LevelMatch(ordered[0].Key.Trim(), ordered[0].Discount, 1, totalLevels);
    }

    private static LevelMatch ResolveLevel(
        IReadOnlyList<LoyaltyLevelEntry> ordered,
        int tenureYears,
        LevelMatch baseLevel)
    {
        var totalLevels = ordered.Count;
        LevelMatch selected = baseLevel;
        for (int i = 0; i < ordered.Count; i++)
        {
            var level = ordered[i];
            if (tenureYears >= level.Years)
            {
                selected = new LevelMatch(level.Key.Trim(), level.Discount, i + 1, totalLevels); // 1-based index
            }
        }

        return selected;
    }

    private static string ComputeLevelsHash(IReadOnlyList<LoyaltyLevelEntry> ordered)
    {
        var builder = new StringBuilder();
        foreach (var level in ordered)
        {
            builder.Append(level.Key.Trim().ToLowerInvariant())
                .Append('|')
                .Append(level.Years)
                .Append('|')
                .Append(level.Discount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                .Append(';');
        }

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Matched level with index information for v3.0 scalable discount system.
    /// </summary>
    /// <param name="Key">Level key name</param>
    /// <param name="Discount">Base discount percentage for this level</param>
    /// <param name="Index">1-based level index (CurrentLevel)</param>
    /// <param name="TotalLevels">Total number of levels in the system</param>
    private readonly record struct LevelMatch(string Key, decimal Discount, int Index, int TotalLevels);
}
