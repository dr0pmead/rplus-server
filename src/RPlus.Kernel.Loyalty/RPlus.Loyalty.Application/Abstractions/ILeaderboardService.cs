using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Abstractions;

/// <summary>
/// Leaderboard entry representing a user's position and points.
/// </summary>
public record LeaderboardEntry(
    Guid UserId,
    int Rank,
    long Points,
    int? RankDelta = null);

/// <summary>
/// User's rank information.
/// </summary>
public record LeaderboardRank(
    Guid UserId,
    int Rank,
    long Points,
    long TotalParticipants);

/// <summary>
/// Service for managing real-time leaderboard using Redis sorted sets.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>
    /// Increment user's score in both monthly and yearly leaderboards.
    /// </summary>
    Task IncrementScoreAsync(Guid userId, long points, int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Get top N users for a specific period.
    /// </summary>
    /// <param name="year">Year</param>
    /// <param name="month">Month (null for yearly)</param>
    /// <param name="count">Number of top users to return</param>
    Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int year, int? month, int count, CancellationToken ct = default);

    /// <summary>
    /// Get user's current rank and score.
    /// </summary>
    Task<LeaderboardRank?> GetUserRankAsync(Guid userId, int year, int? month, CancellationToken ct = default);

    /// <summary>
    /// Rebuild leaderboard from Wallet transactions.
    /// </summary>
    Task<int> RebuildFromWalletAsync(int year, int? month, CancellationToken ct = default);

    /// <summary>
    /// Get total participant count.
    /// </summary>
    Task<long> GetParticipantCountAsync(int year, int? month, CancellationToken ct = default);

    /// <summary>
    /// Rebuild yearly leaderboard from scratch (aggregates all months).
    /// This is a separate operation to ensure consistency - never incrementally update yearly from monthly rebuild.
    /// </summary>
    Task<int> RebuildYearlyAsync(int year, CancellationToken ct = default);
}
