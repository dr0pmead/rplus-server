using System;
using System.Collections.Generic;

namespace RPlus.SDK.Loyalty.Events;

/// <summary>
/// Standard payload describing a domain event that should be evaluated by Loyalty rules.
/// </summary>
public class LoyaltyTriggerEvent
{
    /// <summary>
    /// Domain-specific identifier describing the kind of event (e.g. <c>users.login.success</c>).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// User identifier affected by the event.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Operation id / deduplication key provided by the producer.
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// Free-form metadata provided by the producer. Loyalty rules can match on these values.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Optional raw payload for downstream auditing.
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// External source that generated the trigger.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the originating event.
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
