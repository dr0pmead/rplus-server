using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Application.Abstractions;

namespace RPlus.Loyalty.Api.Controllers;

/// <summary>
/// Leaderboard API for ranking and points aggregation.
/// Authorization is handled by Gateway - this service trusts all incoming requests.
/// </summary>
[ApiController]
[Route("api/loyalty/leaderboard")]
public sealed class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboard;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(
        ILeaderboardService leaderboard,
        ILogger<LeaderboardController> logger)
    {
        _leaderboard = leaderboard;
        _logger = logger;
    }

    /// <summary>
    /// Get top users for a specific period.
    /// GET /api/loyalty/leaderboard?year=2026&amp;month=2&amp;limit=50
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTop(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        try
        {
            var requestYear = year ?? DateTime.UtcNow.Year;
            var clampedLimit = Math.Clamp(limit, 1, 100);

            var entries = await _leaderboard.GetTopAsync(requestYear, month, clampedLimit, ct);
            var total = await _leaderboard.GetParticipantCountAsync(requestYear, month, ct);

            return Ok(new
            {
                year = requestYear,
                month,
                period = month.HasValue ? "monthly" : "yearly",
                totalParticipants = total,
                entries = entries
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get leaderboard");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get user's current rank and score.
    /// GET /api/loyalty/leaderboard/{userId}/rank?year=2026&amp;month=2
    /// </summary>
    [HttpGet("{userId:guid}/rank")]
    public async Task<IActionResult> GetUserRank(
        Guid userId,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        try
        {
            var requestYear = year ?? DateTime.UtcNow.Year;

            var rank = await _leaderboard.GetUserRankAsync(userId, requestYear, month, ct);

            if (rank == null)
            {
                return Ok(new
                {
                    userId,
                    year = requestYear,
                    month,
                    rank = (int?)null,
                    points = 0L,
                    message = "User has no activity for this period"
                });
            }

            return Ok(new
            {
                userId,
                year = requestYear,
                month,
                period = month.HasValue ? "monthly" : "yearly",
                rank = rank.Rank,
                points = rank.Points,
                totalParticipants = rank.TotalParticipants
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rank for user {UserId}", userId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rebuild leaderboard from Wallet transactions.
    /// POST /api/loyalty/leaderboard/rebuild?year=2026&amp;month=2
    /// </summary>
    [HttpPost("rebuild")]
    public async Task<IActionResult> Rebuild(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        try
        {
            var requestYear = year ?? DateTime.UtcNow.Year;

            _logger.LogInformation(
                "Rebuilding leaderboard for {Year}/{Month}",
                requestYear, month?.ToString() ?? "year");

            var count = await _leaderboard.RebuildFromWalletAsync(requestYear, month, ct);

            return Ok(new
            {
                success = true,
                year = requestYear,
                month,
                period = month.HasValue ? "monthly" : "yearly",
                rebuiltUsers = count,
                rebuiltAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild leaderboard");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Rebuild YEARLY leaderboard from scratch (aggregates all months).
    /// POST /api/loyalty/leaderboard/rebuild-yearly?year=2026
    /// </summary>
    [HttpPost("rebuild-yearly")]
    public async Task<IActionResult> RebuildYearly(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        try
        {
            var requestYear = year ?? DateTime.UtcNow.Year;

            _logger.LogInformation("Rebuilding YEARLY leaderboard for {Year}", requestYear);

            var count = await _leaderboard.RebuildYearlyAsync(requestYear, ct);

            return Ok(new
            {
                success = true,
                year = requestYear,
                period = "yearly",
                rebuiltUsers = count,
                rebuiltAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild yearly leaderboard");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint: Simulate activity for a user (for development/testing).
    /// POST /api/loyalty/leaderboard/test-activity
    /// </summary>
    [HttpPost("test-activity")]
    public async Task<IActionResult> TestActivity(
        [FromBody] TestActivityRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var year = request.Year ?? DateTime.UtcNow.Year;
            var month = request.Month ?? DateTime.UtcNow.Month;

            await _leaderboard.IncrementScoreAsync(
                request.UserId,
                request.Points,
                year,
                month,
                ct);

            // Get updated rank
            var rank = await _leaderboard.GetUserRankAsync(request.UserId, year, month, ct);

            return Ok(new
            {
                success = true,
                userId = request.UserId,
                pointsAdded = request.Points,
                year,
                month,
                newRank = rank?.Rank,
                totalPoints = rank?.Points ?? request.Points
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate activity for user {UserId}", request.UserId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint: Trigger monthly snapshot for testing.
    /// POST /api/loyalty/leaderboard/test-snapshot
    /// </summary>
    [HttpPost("test-snapshot")]
    public async Task<IActionResult> TestSnapshot(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromServices] RPlus.Loyalty.Infrastructure.Jobs.MonthlyLeaderboardSnapshotJob? snapshotJob = null,
        CancellationToken ct = default)
    {
        if (snapshotJob == null)
        {
            return StatusCode(500, new { error = "Snapshot job not available" });
        }

        try
        {
            var requestYear = year ?? DateTime.UtcNow.Year;
            var requestMonth = month ?? DateTime.UtcNow.Month;

            var result = await snapshotJob.TriggerAsync(requestYear, requestMonth, ct);

            return Ok(new
            {
                success = result.Success,
                year = requestYear,
                month = requestMonth,
                snapshotsCreated = result.SnapshotsCreated,
                rewardsPending = result.RewardsPending,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger snapshot");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public record TestActivityRequest
{
    public Guid UserId { get; init; }
    public long Points { get; init; } = 100;
    public int? Year { get; init; }
    public int? Month { get; init; }
}
