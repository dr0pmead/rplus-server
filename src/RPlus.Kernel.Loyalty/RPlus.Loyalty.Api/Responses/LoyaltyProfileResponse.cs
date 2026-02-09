using System;

namespace RPlus.Loyalty.Api.Responses;

public class LoyaltyProfileResponse
{
    public Guid UserId { get; set; }
    public decimal PointsBalance { get; set; }
    public Guid? LevelId { get; set; }

    /// <summary>Human-readable loyalty level (bounded context: Loyalty).</summary>
    public string? Level { get; set; }

    /// <summary>v3.0: Current level index (1-based) for ratio calculation.</summary>
    public int CurrentLevel { get; set; } = 1;

    /// <summary>v3.0: Total number of levels in system for ratio calculation.</summary>
    public int TotalLevels { get; set; } = 1;

    /// <summary>Base discount from the current level.</summary>
    public decimal Discount { get; set; }

    /// <summary>Additional motivation discount.</summary>
    public decimal MotivationDiscount { get; set; }

    /// <summary>Total discount (base + motivation).</summary>
    public decimal TotalDiscount { get; set; }

    /// <summary>Tags managed by Loyalty (bounded context: Loyalty).</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
