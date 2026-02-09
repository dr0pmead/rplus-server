using System;

namespace RPlus.SDK.Loyalty.Events;

/// <summary>
/// Event emitted when a user's loyalty profile changes.
/// Published by Loyalty Service after tenure level or motivation tier recalculation.
/// Consumed by Integration Service to update scan cache.
/// 
/// v3.0: Scalable architecture - Loyalty calculates RPlusDiscount, Integration only adds partner part.
/// </summary>
public record LoyaltyProfileUpdatedEvent
{
    /// <summary>
    /// User whose loyalty profile was updated.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Current user's level (1-based index).
    /// Example: 3 means user is on 3rd level out of TotalLevels.
    /// </summary>
    public required int CurrentLevel { get; init; }

    /// <summary>
    /// Total number of levels in the system (from Meta config).
    /// Used by Integration to calculate ProgressRatio = CurrentLevel / TotalLevels.
    /// </summary>
    public required int TotalLevels { get; init; }

    /// <summary>
    /// Ready-to-use RPlus discount percentage (base tenure + motivation bonus).
    /// Calculated by Loyalty Service. Integration just passes it through.
    /// </summary>
    public required decimal RPlusDiscount { get; init; }

    /// <summary>
    /// [Deprecated v2] User's loyalty level (1-5). Use CurrentLevel instead.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// [Deprecated v2] Motivation bonus percentage. Now included in RPlusDiscount.
    /// </summary>
    public decimal MotivationBonus { get; init; }

    /// <summary>
    /// [Deprecated v1] Total combined discount percentage.
    /// </summary>
    public decimal TotalDiscount { get; init; }

    /// <summary>
    /// [Deprecated v1] Base discount from tenure level.
    /// </summary>
    public decimal BaseDiscount { get; init; }

    /// <summary>
    /// [Deprecated v1] Motivation discount from monthly tier.
    /// </summary>
    public decimal MotivationDiscount { get; init; }

    /// <summary>
    /// Current tenure level name (e.g., "Старший сотрудник").
    /// </summary>
    public string? LevelName { get; init; }

    /// <summary>
    /// Timestamp of the update.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Kafka event name constant.
    /// </summary>
    public const string EventName = LoyaltyEventTopics.ProfileUpdated;
}

