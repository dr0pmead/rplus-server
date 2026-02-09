using System;
using System.Collections.Generic;

namespace RPlus.Loyalty.Application.Abstractions;

public sealed record RuntimeGraphAction(string NodeId, string Kind, string DataJson);

public sealed record RuntimeAudienceSelection(string NodeId, string QueryJson, string ResumeFromNodeId);

public sealed record RuntimeGraphRequest(
    Guid RuleId,
    Guid UserId,
    string OperationId,
    string GraphJson,
    string VariablesJson,
    string EventJson,
    DateTime OccurredAtUtc,
    string? StartNodeOverride,
    bool Persist);

public sealed record RuntimeGraphResult(
    bool Success,
    bool Matched,
    decimal PointsDelta,
    IReadOnlyList<string> AwardNodeIds,
    IReadOnlyList<RuntimeGraphAction> Actions,
    RuntimeAudienceSelection? AudienceSelection,
    Guid? ExecutionId,
    string? Error);

public interface IRuntimeGraphClient
{
    Task<RuntimeGraphResult> ExecuteAsync(RuntimeGraphRequest request, CancellationToken ct);
}
