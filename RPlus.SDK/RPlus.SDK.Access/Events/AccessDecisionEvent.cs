using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.Events;

public sealed record AccessDecisionEvent(
    Guid TraceId,
    Guid TenantId,
    Guid UserId,
    string Action,
    Guid? Resource,
    bool Allowed,
    string Reason,
    DateTime OccurredAt,
    Dictionary<string, string>? Context,
    bool StepUpRequired,
    int? RequiredAuthLevel,
    TimeSpan? MaxAuthAge,
    int RiskLevel,
    List<string>? RiskSignals);
