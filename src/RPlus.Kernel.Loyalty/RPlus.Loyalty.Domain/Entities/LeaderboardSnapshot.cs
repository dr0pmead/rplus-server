using System;

namespace RPlus.Loyalty.Domain.Entities;

/// <summary>
/// Historical snapshot of leaderboard rankings.
/// Created at the end of each month/year for rewards distribution.
/// </summary>
public class LeaderboardSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int Year { get; set; }
    
    /// <summary>
    /// Month (1-12). NULL for yearly snapshots.
    /// </summary>
    public int? Month { get; set; }
    
    public long FinalPoints { get; set; }
    public int FinalRank { get; set; }
    
    /// <summary>
    /// Type of reward: "discount", "points", "prize"
    /// </summary>
    public string? RewardType { get; set; }
    
    /// <summary>
    /// Reward value (percentage, points amount, or prize name)
    /// </summary>
    public string? RewardValue { get; set; }
    
    /// <summary>
    /// Whether the reward has been distributed.
    /// </summary>
    public bool RewardDistributed { get; set; }
    
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
}
