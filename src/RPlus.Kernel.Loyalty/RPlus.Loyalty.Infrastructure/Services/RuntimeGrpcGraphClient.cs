using Google.Protobuf.WellKnownTypes;
using RPlus.Loyalty.Application.Abstractions;
using RPlusGrpc.Runtime;
using RuntimeAudienceSelectionDto = RPlus.Loyalty.Application.Abstractions.RuntimeAudienceSelection;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class RuntimeGrpcGraphClient : IRuntimeGraphClient
{
    private readonly RuntimeService.RuntimeServiceClient _client;

    public RuntimeGrpcGraphClient(RuntimeService.RuntimeServiceClient client)
    {
        _client = client;
    }

    public async Task<RuntimeGraphResult> ExecuteAsync(RuntimeGraphRequest request, CancellationToken ct)
    {
        var grpcRequest = new ExecuteGraphRequest
        {
            RuleId = request.RuleId.ToString(),
            UserId = request.UserId == Guid.Empty ? string.Empty : request.UserId.ToString(),
            OperationId = request.OperationId ?? string.Empty,
            GraphJson = request.GraphJson ?? string.Empty,
            VariablesJson = request.VariablesJson ?? string.Empty,
            EventJson = request.EventJson ?? string.Empty,
            OccurredAt = Timestamp.FromDateTime(request.OccurredAtUtc.ToUniversalTime()),
            StartNodeOverride = request.StartNodeOverride ?? string.Empty
        };

        var response = request.Persist
            ? await _client.ExecuteGraphAsync(grpcRequest, cancellationToken: ct)
            : await _client.SimulateGraphAsync(grpcRequest, cancellationToken: ct);

        if (!response.Success)
        {
            return new RuntimeGraphResult(false, false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null, null, response.Error);
        }

        RuntimeAudienceSelectionDto? audience = null;
        if (response.Audience != null && !string.IsNullOrWhiteSpace(response.Audience.NodeId))
        {
            audience = new RuntimeAudienceSelectionDto(response.Audience.NodeId, response.Audience.QueryJson, response.Audience.ResumeFromNodeId);
        }

        var actions = response.Actions
            .Select(a => new RuntimeGraphAction(a.NodeId, a.Kind, a.DataJson))
            .ToList();

        Guid? executionId = null;
        if (!string.IsNullOrWhiteSpace(response.Execution?.ExecutionId) && Guid.TryParse(response.Execution.ExecutionId, out var parsedId))
        {
            executionId = parsedId;
        }

        var points = response.Execution?.PointsDelta ?? 0;

        return new RuntimeGraphResult(
            Success: true,
            Matched: response.Execution?.Matched ?? false,
            PointsDelta: (decimal)points,
            AwardNodeIds: response.AwardNodeIds.ToList(),
            Actions: actions,
            AudienceSelection: audience,
            ExecutionId: executionId,
            Error: null);
    }
}
