using System;

namespace RPlus.Loyalty.Domain.Entities;

public class LoyaltyRuleState
{
    public Guid RuleId { get; set; }
    public Guid UserId { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

