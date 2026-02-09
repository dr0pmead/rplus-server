using System;
using RPlus.SDK.Security.Enums;
using RPlus.SDK.Security.Models;

namespace RPlus.SDK.Contracts.Domain.Security;

// Signals IN
public record SecuritySignalReceived_v1(
    string SignalType,
    SecuritySubject Subject,
    string Details,
    double Score
) : IntegrationEvent;

// Decisions OUT
public record SecurityDecisionMade_v1(
    SecuritySubject Subject,
    SecurityDecision Decision,
    string Route,
    string RequestId
) : IntegrationEvent;

// Challenges
public record SecurityChallengeIssued_v1(
    string ChallengeId,
    SecuritySubject Subject,
    ChallengeType Type,
    int Difficulty
) : IntegrationEvent;

public record SecurityChallengePassed_v1(
    string ChallengeId,
    SecuritySubject Subject,
    string Proof
) : IntegrationEvent;

// Threat Lifecycle
public record SecurityThreatDetected_v1(
    SecuritySubject Subject,
    string ThreatType,
    ThreatLevel Level,
    string Evidence
) : IntegrationEvent;

public record SecurityThreatLevelChanged_v1(
    SecuritySubject Subject,
    ThreatLevel OldLevel,
    ThreatLevel NewLevel,
    string Reason
) : IntegrationEvent;

// Blocking/Throttling
public record SecuritySubjectBlocked_v1(
    SecuritySubject Subject,
    TimeSpan Duration,
    string Reason
) : IntegrationEvent;

public record SecuritySubjectUnblocked_v1(
    SecuritySubject Subject,
    string Reason
) : IntegrationEvent;

public record SecuritySubjectThrottled_v1(
    SecuritySubject Subject,
    string Route,
    int RateLimit,
    int WindowSeconds
) : IntegrationEvent;
