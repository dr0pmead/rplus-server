using System;

namespace RPlus.Loyalty.Domain.Entities;

/// <summary>
/// Stores the last tenure recalculation metadata (hash + last run time).
/// </summary>
public sealed class LoyaltyTenureState
{
    public string Key { get; set; } = "system.loyalty.tenure.level";
    public string? LevelsHash { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
}
