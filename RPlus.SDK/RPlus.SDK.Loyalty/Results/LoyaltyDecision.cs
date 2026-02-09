using System.Collections.Generic;
using RPlus.SDK.Loyalty.Models;

namespace RPlus.SDK.Loyalty.Results;

public class LoyaltyDecision
{
    public decimal PointsDelta { get; set; }
    public List<LoyaltyRule> AppliedRules { get; set; } = new();
}
