using System;

namespace RPlus.Loyalty.Domain.Entities;

/// <summary>
/// Persistent per-user per-graph-node state used by stateful graph nodes (Counter, Cooldown, etc).
/// Binding is: (RuleId, UserId, NodeId).
/// </summary>
public sealed class LoyaltyGraphNodeState
{
    public Guid RuleId { get; set; }

    public Guid UserId { get; set; }

    public string NodeId { get; set; } = string.Empty;

    /// <summary>Arbitrary JSON state stored as JSONB.</summary>
    public string StateJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

