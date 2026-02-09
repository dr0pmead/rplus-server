using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using StackExchange.Redis;

namespace RPlus.Loyalty.Infrastructure.Jobs;

/// <summary>
/// Monthly job that snapshots leaderboard rankings and distributes rewards.
/// Runs on the 1st of each month at 00:05 to process the previous month.
/// After snapshot, resets the monthly Redis leaderboard.
/// </summary>
public sealed class MonthlyLeaderboardSnapshotJob : IHostedService
{
    private const string LeaderboardKeyPrefix = "rplus:leaderboard";
    
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonthlyLeaderboardSnapshotJob> _logger;
    private Timer? _timer;

    public MonthlyLeaderboardSnapshotJob(
        IServiceProvider serviceProvider,
        ILogger<MonthlyLeaderboardSnapshotJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Calculate delay until next 1st of month at 00:05
        var now = DateTime.UtcNow;
        var nextRun = new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddMonths(1);
        }
        var delay = nextRun - now;

        _logger.LogInformation(
            "MonthlyLeaderboardSnapshotJob scheduled for {NextRun} (in {Delay})",
            nextRun, delay);

        _timer = new Timer(
            _ => _ = ExecuteAsync(CancellationToken.None),
            null,
            delay,
            TimeSpan.FromDays(28)); // Roughly monthly, will recalculate each run

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Manually trigger the snapshot job (for testing).
    /// </summary>
    public async Task<SnapshotResult> TriggerAsync(int year, int month, CancellationToken ct = default)
    {
        return await ExecuteForMonthAsync(year, month, resetAfterSnapshot: false, ct);
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        // Process previous month
        var targetDate = DateTime.UtcNow.AddMonths(-1);
        await ExecuteForMonthAsync(targetDate.Year, targetDate.Month, resetAfterSnapshot: true, ct);
    }

    private async Task<SnapshotResult> ExecuteForMonthAsync(int year, int month, bool resetAfterSnapshot, CancellationToken ct)
    {
        _logger.LogInformation("Starting monthly leaderboard snapshot for {Year}/{Month}", year, month);

        using var scope = _serviceProvider.CreateScope();
        var leaderboard = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LoyaltyDbContext>>();
        var rewardCatalog = scope.ServiceProvider.GetService<ILeaderboardRewardCatalog>();
        var rewardDistributor = scope.ServiceProvider.GetService<Services.IRewardDistributor>();
        var redis = scope.ServiceProvider.GetService<IConnectionMultiplexer>();

        try
        {
            // Get top users from Redis
            var topUsers = await leaderboard.GetTopAsync(year, month, 100, ct);

            if (topUsers.Count == 0)
            {
                _logger.LogWarning("No users in leaderboard for {Year}/{Month}", year, month);
                return new SnapshotResult(true, 0, 0);
            }

            // Get reward configuration
            var rewards = rewardCatalog != null
                ? await rewardCatalog.GetMonthlyRewardsAsync(ct)
                : new List<LeaderboardRewardConfig>();

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var snapshotsCreated = 0;
            var rewardsDistributed = 0;

            foreach (var entry in topUsers)
            {
                // Check if snapshot already exists
                var existing = await db.LeaderboardSnapshots
                    .FirstOrDefaultAsync(s => 
                        s.UserId == entry.UserId && 
                        s.Year == year && 
                        s.Month == month, ct);

                if (existing != null)
                    continue;

                // Create snapshot
                var snapshot = new LeaderboardSnapshot
                {
                    UserId = entry.UserId,
                    Year = year,
                    Month = month,
                    FinalPoints = entry.Points,
                    FinalRank = entry.Rank,
                    SnapshotAt = DateTime.UtcNow
                };

                // Check for rewards and distribute them
                var reward = rewards.Find(r => r.Rank == entry.Rank);
                if (reward != null)
                {
                    snapshot.RewardType = reward.RewardType;
                    snapshot.RewardValue = reward.Value;
                    
                    // Actually distribute the reward
                    if (rewardDistributor != null)
                    {
                        try
                        {
                            snapshot.RewardDistributed = await rewardDistributor.DistributeAsync(
                                entry.UserId, 
                                reward.RewardType, 
                                reward.Value, 
                                ct);
                            
                            if (snapshot.RewardDistributed)
                                rewardsDistributed++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, 
                                "Failed to distribute {RewardType} reward to user {UserId}", 
                                reward.RewardType, entry.UserId);
                            snapshot.RewardDistributed = false;
                        }
                    }
                }

                db.LeaderboardSnapshots.Add(snapshot);
                snapshotsCreated++;
            }

            await db.SaveChangesAsync(ct);


            _logger.LogInformation(
                "Monthly leaderboard snapshot completed for {Year}/{Month}: {Snapshots} snapshots, {Rewards} pending rewards",
                year, month, snapshotsCreated, rewardsDistributed);

            // Reset the monthly leaderboard in Redis (users start fresh each month)
            if (resetAfterSnapshot && redis != null)
            {
                await ResetMonthlyLeaderboardAsync(redis, year, month);
            }

            return new SnapshotResult(true, snapshotsCreated, rewardsDistributed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create monthly leaderboard snapshot for {Year}/{Month}", year, month);
            return new SnapshotResult(false, 0, 0, ex.Message);
        }
    }

    private async Task ResetMonthlyLeaderboardAsync(IConnectionMultiplexer redis, int year, int month)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"{LeaderboardKeyPrefix}:{year}:{month:D2}";
            
            await db.KeyDeleteAsync(key);
            
            _logger.LogInformation(
                "Monthly leaderboard reset completed for {Year}/{Month} (key: {Key})",
                year, month, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset monthly leaderboard for {Year}/{Month}", year, month);
        }
    }
}

public record SnapshotResult(bool Success, int SnapshotsCreated, int RewardsPending, string? Error = null);

/// <summary>
/// Interface for reward configuration catalog.
/// </summary>
public interface ILeaderboardRewardCatalog
{
    Task<List<LeaderboardRewardConfig>> GetMonthlyRewardsAsync(CancellationToken ct = default);
    Task<List<LeaderboardRewardConfig>> GetYearlyRewardsAsync(CancellationToken ct = default);
}

public record LeaderboardRewardConfig(int Rank, string RewardType, string Value);
