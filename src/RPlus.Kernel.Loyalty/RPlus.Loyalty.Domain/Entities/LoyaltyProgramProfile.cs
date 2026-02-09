using System;

namespace RPlus.Loyalty.Domain.Entities;

/// <summary>
/// Loyalty-owned master record for program status and tags (bounded context: Loyalty).
/// This is the canonical place for loyalty level/tags and points balance.
/// </summary>
public sealed class LoyaltyProgramProfile
{
    /// <summary>Primary key.</summary>
    public Guid UserId { get; set; }

    /// <summary>Program level (e.g. Bronze/Silver/Gold).</summary>
    public string? Level { get; set; }

    /// <summary>JSON array of tags (e.g. ["vip","birthday-eligible"]).</summary>
    public string TagsJson { get; set; } = "[]";

    public decimal PointsBalance { get; set; }

    /// <summary>Base discount for the user (from tenure/level).</summary>
    public decimal Discount { get; set; }

    /// <summary>Additional motivation discount (manual/extra).</summary>
    public decimal MotivationDiscount { get; set; }

    /// <summary>Convenience computed total discount.</summary>
    public decimal TotalDiscount => Discount + MotivationDiscount;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
