using System;
using RPlus.SDK.Loyalty.Models;

#nullable enable
namespace RPlus.Loyalty.Domain.Entities;

public class LoyaltyLevel : RPlus.SDK.Loyalty.Models.LoyaltyLevel
{
    public string Benefits { get; set; } = string.Empty;
}
