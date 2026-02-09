using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RPlus.Kernel.Runtime.Application.Services;
using RPlusGrpc.Runtime;

namespace RPlus.Kernel.Runtime.API.Services;

public sealed class RuntimeGrpcService : RuntimeService.RuntimeServiceBase
{
    private readonly RuntimeGraphExecutionService _executor;

    public RuntimeGrpcService(RuntimeGraphExecutionService executor)
    {
        _executor = executor;
    }

    public override Task<ExecuteGraphResponse> ExecuteGraph(ExecuteGraphRequest request, ServerCallContext context)
        => HandleExecute(request, persist: true, context.CancellationToken);

    public override Task<ExecuteGraphResponse> SimulateGraph(ExecuteGraphRequest request, ServerCallContext context)
        => HandleExecute(request, persist: false, context.CancellationToken);

    private async Task<ExecuteGraphResponse> HandleExecute(ExecuteGraphRequest request, bool persist, CancellationToken ct)
    {
        if (!Guid.TryParse(request.RuleId, out var ruleId))
        {
            return new ExecuteGraphResponse { Success = false, Error = "invalid_rule_id" };
        }

        var userId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(request.UserId) && !Guid.TryParse(request.UserId, out userId))
        {
            return new ExecuteGraphResponse { Success = false, Error = "invalid_user_id" };
        }

        var occurredAt = request.OccurredAt?.ToDateTime() ?? DateTime.UtcNow;

        var execRequest = new RuntimeGraphRequest(
            RuleId: ruleId,
            UserId: userId,
            OperationId: request.OperationId ?? string.Empty,
            GraphJson: request.GraphJson ?? string.Empty,
            VariablesJson: request.VariablesJson ?? string.Empty,
            EventJson: request.EventJson ?? string.Empty,
            OccurredAtUtc: occurredAt,
            StartNodeOverride: string.IsNullOrWhiteSpace(request.StartNodeOverride) ? null : request.StartNodeOverride,
            Persist: persist);

        var result = await _executor.ExecuteAsync(execRequest, ct);

        var response = new ExecuteGraphResponse
        {
            Success = result.Success,
            Error = result.Error ?? string.Empty,
        };

        if (!result.Success)
        {
            return response;
        }

        if (result.AudienceSelection != null)
        {
            response.Audience = new RuntimeAudienceSelection
            {
                NodeId = result.AudienceSelection.NodeId,
                QueryJson = result.AudienceSelection.QueryJson,
                ResumeFromNodeId = result.AudienceSelection.ResumeFromNodeId
            };
        }

        response.Execution = new RuntimeExecution
        {
            ExecutionId = result.ExecutionId?.ToString() ?? string.Empty,
            Matched = result.Matched,
            PointsDelta = Convert.ToDouble(result.PointsDelta, CultureInfo.InvariantCulture)
        };

        response.AwardNodeIds.AddRange(result.AwardNodeIds);
        foreach (var action in result.Actions)
        {
            response.Actions.Add(new RuntimeAction
            {
                NodeId = action.NodeId,
                Kind = action.Kind,
                DataJson = action.DataJson
            });
        }

        return response;
    }
}
