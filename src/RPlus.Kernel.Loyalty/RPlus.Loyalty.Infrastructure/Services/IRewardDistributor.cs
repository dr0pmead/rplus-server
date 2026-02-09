using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Infrastructure.Services;

/// <summary>
/// Interface for distributing leaderboard rewards to users.
/// </summary>
public interface IRewardDistributor
{
    /// <summary>
    /// Distribute a reward to a user.
    /// </summary>
    /// <param name="userId">The user to reward</param>
    /// <param name="rewardType">Type of reward: "points", "discount", "prize"</param>
    /// <param name="value">Reward value (points amount, discount %, or prize name)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if reward was successfully distributed</returns>
    Task<bool> DistributeAsync(Guid userId, string rewardType, string value, CancellationToken ct = default);
}
