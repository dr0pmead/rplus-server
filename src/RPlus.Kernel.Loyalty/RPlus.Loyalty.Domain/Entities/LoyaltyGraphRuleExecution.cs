using System;

namespace RPlus.Loyalty.Domain.Entities;

public sealed class LoyaltyGraphRuleExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RuleId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Stable per-event operation id (dedupe key). Typically envelope EventId or a deterministic hash.
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    public decimal PointsApplied { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

