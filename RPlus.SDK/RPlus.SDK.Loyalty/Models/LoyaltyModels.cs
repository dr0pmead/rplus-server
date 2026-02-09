using System;

#nullable enable
namespace RPlus.SDK.Loyalty.Models;

public class LoyaltyRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public class LoyaltyLevel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinSpl { get; set; }
    public decimal CashbackPercentage { get; set; }
}

public class LoyaltyProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal PointsBalance { get; set; }
    public Guid LevelId { get; set; }
    public DateTime LastActivityAt { get; set; }
}
