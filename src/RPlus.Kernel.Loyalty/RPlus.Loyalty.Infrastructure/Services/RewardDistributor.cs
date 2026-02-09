using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Persistence;
using RPlusGrpc.Wallet;

namespace RPlus.Loyalty.Infrastructure.Services;

/// <summary>
/// Distributes leaderboard rewards to users.
/// Handles: points (via Wallet gRPC), discount (via LoyaltyProgramProfile), prize (logging only).
/// </summary>
public sealed class RewardDistributor : IRewardDistributor
{
    private readonly WalletService.WalletServiceClient _walletClient;
    private readonly IDbContextFactory<LoyaltyDbContext> _dbFactory;
    private readonly ILogger<RewardDistributor> _logger;

    public RewardDistributor(
        WalletService.WalletServiceClient walletClient,
        IDbContextFactory<LoyaltyDbContext> dbFactory,
        ILogger<RewardDistributor> logger)
    {
        _walletClient = walletClient;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<bool> DistributeAsync(Guid userId, string rewardType, string value, CancellationToken ct = default)
    {
        try
        {
            var type = rewardType.ToLowerInvariant();
            
            return type switch
            {
                "points" => await DistributePointsAsync(userId, value, ct),
                "discount" => await DistributeDiscountAsync(userId, value, ct),
                "prize" => DistributePrize(userId, value),
                _ => LogUnknownType(rewardType)
            };
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error distributing {RewardType} reward to user {UserId}", rewardType, userId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to distribute {RewardType} reward to user {UserId}", rewardType, userId);
            return false;
        }
    }

    /// <summary>
    /// Distribute points reward via Wallet gRPC.
    /// </summary>
    private async Task<bool> DistributePointsAsync(Guid userId, string value, CancellationToken ct)
    {
        if (!long.TryParse(value, out var points) || points <= 0)
        {
            _logger.LogError("Invalid points value for reward: {Value}", value);
            return false;
        }

        var request = new AccruePointsRequest
        {
            UserId = userId.ToString(),
            Amount = points,
            OperationId = $"leaderboard-reward-{userId:N}-{DateTime.UtcNow:yyyyMMdd}",
            Source = "leaderboard_reward",
            SourceType = "reward",
            SourceCategory = "monthly_leaderboard",
            Description = $"Monthly Leaderboard Reward: {points} points",
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var response = await _walletClient.AccruePointsAsync(request, cancellationToken: ct);

        if (response.Status == "completed" || response.Status == "success")
        {
            _logger.LogInformation(
                "Points reward distributed: {Points} points to user {UserId}, new balance: {Balance}",
                points, userId, response.BalanceAfter);
            return true;
        }

        _logger.LogWarning(
            "Points reward failed for user {UserId}: status={Status}, error={Error}",
            userId, response.Status, response.ErrorCode);
        return false;
    }

    /// <summary>
    /// Distribute discount reward by updating LoyaltyProgramProfile.MotivationDiscount.
    /// </summary>
    private async Task<bool> DistributeDiscountAsync(Guid userId, string value, CancellationToken ct)
    {
        if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var discountDelta) || discountDelta <= 0)
        {
            _logger.LogError("Invalid discount value for reward: {Value}", value);
            return false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var profile = await db.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile == null)
        {
            _logger.LogWarning("User profile {UserId} not found for discount reward.", userId);
            return false;
        }

        var oldDiscount = profile.MotivationDiscount;
        profile.MotivationDiscount += discountDelta;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Discount reward distributed: +{Discount}% to user {UserId} (was {Old}%, now {New}%)",
            discountDelta, userId, oldDiscount, profile.MotivationDiscount);
        return true;
    }

    /// <summary>
    /// Log physical prize for admin fulfillment.
    /// </summary>
    private bool DistributePrize(Guid userId, string value)
    {
        // Physical prizes require manual admin action
        _logger.LogInformation(
            "🎁 PRIZE AWARDED: User {UserId} won '{Prize}'. Manual fulfillment required by admin.",
            userId, value);
        
        // TODO: Future enhancement - send notification to admin, create task in admin panel
        return true;
    }

    private bool LogUnknownType(string rewardType)
    {
        _logger.LogWarning("Unknown reward type: {Type}. No distribution performed.", rewardType);
        return false;
    }
}
