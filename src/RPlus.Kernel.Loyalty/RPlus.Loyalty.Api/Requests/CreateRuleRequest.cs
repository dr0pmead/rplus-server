using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RPlus.Loyalty.Api.Requests;

public class CreateRuleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string EventType { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Points { get; set; }

    public int Priority { get; set; } = 10;

    public bool IsActive { get; set; } = true;

    public Dictionary<string, string> MetadataFilter { get; set; } = new();

    public string? Description { get; set; }

    /// <summary>
    /// Optional rule engine type. Defaults to <c>simple_points</c>.
    /// Supported: <c>simple_points</c>, <c>streak_days</c>, <c>count_within_window</c>.
    /// </summary>
    public string? RuleType { get; set; }

    /// <summary>
    /// Optional JSON configuration for <see cref="RuleType"/>. Stored as JSONB.
    /// </summary>
    public string? RuleConfigJson { get; set; }
}
