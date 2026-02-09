using System.Collections.Generic;

#nullable enable

namespace RPlus.SDK.Access.Models;

public enum RiskLevel
{
    Low = 0,
    Elevated = 1,
    High = 2,
    Critical = 3
}

public class RiskSignal
{
    public string Code { get; set; } = string.Empty;
    public RiskLevel Severity { get; set; }
    public string Value { get; set; } = string.Empty;

    public RiskSignal() { }
    public RiskSignal(string code, RiskLevel severity, string value)
    {
        Code = code;
        Severity = severity;
        Value = value;
    }
}

public class RiskAssessment
{
    public RiskLevel Level { get; set; }
    public List<RiskSignal> Signals { get; set; } = new List<RiskSignal>();
    public int? RecommendedAal { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
