using System;

namespace RPlus.Loyalty.Domain.Entities;

public sealed class LoyaltyGraphRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human readable name for the Node Editor.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kafka topic this graph listens to (as discovered from Schema Registry).
    /// Example: <c>auth.user.logged_in.v1</c>.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    public int Priority { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    /// <summary>Optional cap on total executions across all users. Null = unlimited.</summary>
    public int? MaxExecutions { get; set; }

    /// <summary>How many times this rule has been executed (matched).</summary>
    public int ExecutionsCount { get; set; }

    /// <summary>System rules are locked (graph cannot be edited) and only variables can be changed.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Stable system identifier, e.g. system.tenure.level.</summary>
    public string? SystemKey { get; set; }

    /// <summary>
    /// JSON graph definition (nodes + edges). Stored as JSONB in Postgres.
    /// </summary>
    public string GraphJson { get; set; } = "{}";

    /// <summary>Editable variables for system rules (JSONB).</summary>
    public string VariablesJson { get; set; } = "{}";

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
