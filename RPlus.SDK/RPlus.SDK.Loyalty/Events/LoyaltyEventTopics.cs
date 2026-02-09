namespace RPlus.SDK.Loyalty.Events;

/// <summary>
/// Canonical Kafka topic names that the Loyalty service publishes to or consumes from.
/// Keeping them in the SDK maintains a single source of truth for other services.
/// </summary>
public static class LoyaltyEventTopics
{
    /// <summary>
    /// Topic accepting trigger events emitted by other services to request loyalty evaluation.
    /// </summary>
    public const string Triggered = "loyalty.events.triggered.v1";

    /// <summary>
    /// Topic emitted when Loyalty requests the Wallet service to accrue points.
    /// </summary>
    public const string PointsAccrualRequested = "loyalty.points.accrual.requested.v1";

    /// <summary>
    /// Topic emitted once Loyalty has applied rules and updated its internal profile.
    /// </summary>
    public const string PointsAccrued = "loyalty.points.accrued.v1";

    /// <summary>
    /// Topic emitted when Loyalty rejects an event due to validation/rule failures.
    /// </summary>
    public const string PointsAccrualFailed = "loyalty.points.accrual.failed.v1";

    /// <summary>
    /// Topic for activity completion events from various services (HR, Training, CRM).
    /// Used by Leaderboard system to track user activities.
    /// </summary>
    public const string ActivityCompleted = "loyalty.activity.completed.v1";

    /// <summary>
    /// Topic emitted when a user's loyalty profile is updated (discount changes).
    /// Consumed by Integration service to update scan cache.
    /// </summary>
    public const string ProfileUpdated = "loyalty.profile.updated.v1";
}
