using System;

namespace RPlus.SDK.Loyalty.Events;

/// <summary>
/// Event emitted when an employee completes an activity that earns points.
/// Published by HR, Training, CRM, and other services.
/// Consumed by Leaderboard and Wallet services.
/// </summary>
public record ActivityCompletedEvent
{
    /// <summary>
    /// User who completed the activity.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Unique identifier of the activity (course ID, event ID, etc.).
    /// </summary>
    public required Guid ActivityId { get; init; }

    /// <summary>
    /// Type of activity: training, quality, event, achievement, certification, onboarding, anniversary.
    /// </summary>
    public required string ActivityType { get; init; }

    /// <summary>
    /// Source service that published the event: hr, lms, crm, gamification.
    /// </summary>
    public required string ActivitySource { get; init; }

    /// <summary>
    /// Points awarded for this activity.
    /// </summary>
    public required long Points { get; init; }

    /// <summary>
    /// Human-readable title of the activity (e.g., "Курс: Безопасность").
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional description of the activity.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When the activity was completed.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Operation ID for idempotency.
    /// </summary>
    public string OperationId { get; init; } = Guid.NewGuid().ToString();
}
