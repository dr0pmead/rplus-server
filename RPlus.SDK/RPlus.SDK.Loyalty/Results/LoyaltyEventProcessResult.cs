using System.Collections.Generic;

namespace RPlus.SDK.Loyalty.Results;

/// <summary>
/// Outcome returned by the Loyalty engine after evaluating a trigger.
/// </summary>
public class LoyaltyEventProcessResult
{
    /// <summary>
    /// True when the event produced a non-zero point delta and has been persisted.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Delta applied to the loyalty profile during this evaluation.
    /// </summary>
    public decimal PointsDelta { get; set; }

    /// <summary>
    /// Resulting profile balance.
    /// </summary>
    public decimal NewBalance { get; set; }

    /// <summary>
    /// Applied rule identifiers for analytics/debugging.
    /// </summary>
    public List<string> AppliedRuleIds { get; set; } = new();

    /// <summary>
    /// Optional technical failure code when processing was skipped.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Optional human readable description corresponding to <see cref="ErrorCode"/>.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
