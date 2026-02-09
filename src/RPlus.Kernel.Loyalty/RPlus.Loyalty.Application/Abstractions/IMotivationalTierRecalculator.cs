using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Abstractions;

/// <summary>
/// Request for motivational tier recalculation.
/// </summary>
/// <param name="Force">If true, force recalculation even if already run this month</param>
/// <param name="Year">Year to calculate for (default: current)</param>
/// <param name="Month">Month to calculate for (default: previous month)</param>
/// <param name="BatchSize">Number of users to process per batch</param>
/// <param name="MaxParallel">Maximum parallelism for Wallet calls</param>
public sealed record MotivationalRecalcRequest(
    bool Force = false,
    int? Year = null,
    int? Month = null,
    int BatchSize = 500,
    int MaxParallel = 8);

/// <summary>
/// Result of motivational tier recalculation.
/// </summary>
public sealed record MotivationalRecalcResult(
    bool Success,
    int TotalUsers,
    int UpdatedUsers,
    string? TiersHash,
    bool Skipped,
    string? Error);

/// <summary>
/// Result of single user motivational tier recalculation.
/// </summary>
public sealed record SingleUserMotivationalResult(
    bool Success,
    string? TierKey,
    decimal Discount,
    long MonthlyPoints,
    bool Updated,
    string? Error);

/// <summary>
/// Recalculator for monthly motivational discount tiers.
/// Fetches monthly points from Wallet and assigns appropriate tier.
/// </summary>
public interface IMotivationalTierRecalculator
{
    /// <summary>
    /// Recalculate motivational tiers for all users.
    /// Should be run monthly (typically at start of new month for previous month).
    /// </summary>
    Task<MotivationalRecalcResult> RecalculateAsync(MotivationalRecalcRequest request, CancellationToken ct = default);

    /// <summary>
    /// Recalculate motivational tier for a single user.
    /// </summary>
    Task<SingleUserMotivationalResult> RecalculateUserAsync(Guid userId, int year, int month, CancellationToken ct = default);
}
