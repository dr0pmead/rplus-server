using System;
using System.Collections.Generic;
using RPlus.SDK.Security.Enums;

namespace RPlus.SDK.Security.Models;

public record SecuritySubject
(
    string IpAddress,
    string? DeviceId = null,
    string? Fingerprint = null,
    string? UserId = null,
    string? TenantId = null
);

public record SecurityContext
(
   SecuritySubject Subject,
   string Route,
   Dictionary<string, string> Headers,
   string RequestId,
   string? UserAgent = null,
   Dictionary<string, string>? ClientHints = null
);

public record SecurityDecision
(
    DecisionType Type,
    ThreatLevel ThreatLevel,
    DecisionSource Source,
    string ReasonCode,
    bool IsTerminal,
    ThreatLevel EffectiveThreatLevel,
    string? PolicyId = null,
    int? TtlSeconds = null
);

public record SecurityChallenge
(
    string ChallengeId,
    ChallengeType Type,
    int Difficulty,
    DateTime ExpiresAt
);
