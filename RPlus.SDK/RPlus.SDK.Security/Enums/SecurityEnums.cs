[assembly: System.Runtime.InteropServices.Guid("e078f7e2-1a40-424b-b2f5-b6d4f6c8d769")]

namespace RPlus.SDK.Security.Enums;

public enum DecisionType
{
    Allow = 0,
    Throttle = 1,
    Challenge = 2,
    Block = 3
}

public enum ThreatLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ChallengeType
{
    ProofOfWork = 0,
    Behavioral = 1,
    Hybrid = 2
}

public enum DecisionSource
{
    RateLimit = 0,
    BotDetection = 1,
    Manual = 2,
    Escalation = 3,
    Cache = 4
}
