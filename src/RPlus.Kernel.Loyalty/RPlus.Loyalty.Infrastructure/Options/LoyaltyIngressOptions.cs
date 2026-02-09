using System.Collections.Generic;

namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltyIngressOptions
{
    public string[] Topics { get; set; } = [];

    /// <summary>
    /// Per-topic mapping configuration (topic name -> mapping).
    /// </summary>
    public Dictionary<string, LoyaltyIngressMapping> Mappings { get; set; } = new();
}

public sealed class LoyaltyIngressMapping
{
    /// <summary>
    /// Internal loyalty trigger event type used by rules (e.g. <c>users.login.success</c>).
    /// </summary>
    public string TriggerEventType { get; set; } = string.Empty;

    /// <summary>
    /// JSON path to a user id string (supports dot notation, case-insensitive).
    /// Examples: <c>UserId</c>, <c>Payload.UserId</c>, <c>AggregateId</c>.
    /// </summary>
    public string UserIdPath { get; set; } = "UserId";

    /// <summary>
    /// Optional JSON path to an RFC3339 timestamp. If missing, uses envelope OccurredAt or now.
    /// </summary>
    public string? OccurredAtPath { get; set; }

    /// <summary>
    /// Optional JSON path to a stable operation id. If missing, falls back to envelope EventId or a content hash.
    /// </summary>
    public string? OperationIdPath { get; set; }

    /// <summary>
    /// Optional fixed source name for triggers. If empty, will use envelope Source (if present) or topic name.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Metadata projection: metadata key -> json path in the incoming message.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

