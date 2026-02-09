using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RPlus.Loyalty.Api.Requests;

public class UpdateRuleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string EventType { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Points { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public Dictionary<string, string> MetadataFilter { get; set; } = new();

    public string? Description { get; set; }

    public string? RuleType { get; set; }

    public string? RuleConfigJson { get; set; }
}
