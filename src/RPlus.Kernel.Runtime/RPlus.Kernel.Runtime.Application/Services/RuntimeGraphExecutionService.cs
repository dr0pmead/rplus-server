using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Runtime.Application.Graph;
using RPlus.Kernel.Runtime.Domain.Entities;
using RPlus.Kernel.Runtime.Persistence;

namespace RPlus.Kernel.Runtime.Application.Services;

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
    RuntimeGraphAudienceSelection? AudienceSelection,
    Guid? ExecutionId,
    string? Error);

public sealed class RuntimeGraphExecutionService
{
    private readonly RuntimeDbContext _db;
    private readonly RuntimeGraphExecutor _executor;

    public RuntimeGraphExecutionService(RuntimeDbContext db, RuntimeGraphExecutor executor)
    {
        _db = db;
        _executor = executor;
    }

    public async Task<RuntimeGraphResult> ExecuteAsync(RuntimeGraphRequest request, CancellationToken ct)
    {
        if (request.RuleId == Guid.Empty)
        {
            return new RuntimeGraphResult(false, false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null, null, "rule_id_required");
        }

        if (string.IsNullOrWhiteSpace(request.OperationId))
        {
            return new RuntimeGraphResult(false, false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null, null, "operation_id_required");
        }

        if (string.IsNullOrWhiteSpace(request.GraphJson))
        {
            return new RuntimeGraphResult(false, false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null, null, "graph_json_required");
        }

        JsonDocument contextDoc;
        try
        {
            contextDoc = string.IsNullOrWhiteSpace(request.EventJson)
                ? JsonDocument.Parse("{}")
                : JsonDocument.Parse(request.EventJson);
        }
        catch (Exception)
        {
            return new RuntimeGraphResult(false, false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null, null, "invalid_event_json");
        }

        using var _ = contextDoc;

        if (request.Persist)
        {
            var existing = await _db.GraphExecutions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.RuleId == request.RuleId && x.UserId == request.UserId && x.OperationId == request.OperationId, ct);
            if (existing != null)
            {
                return new RuntimeGraphResult(true, existing.Matched, existing.PointsDelta, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null, existing.Id, null);
            }
        }

        var stateById = new Dictionary<string, RuntimeGraphNodeState>(StringComparer.OrdinalIgnoreCase);
        if (request.Persist)
        {
            var states = await _db.GraphNodeStates
                .Where(x => x.RuleId == request.RuleId && x.UserId == request.UserId)
                .ToListAsync(ct);
            foreach (var state in states)
            {
                stateById[state.NodeId] = state;
            }
        }

        RuntimeGraphNodeState GetNodeState(string nodeId)
        {
            if (stateById.TryGetValue(nodeId, out var existingState))
            {
                return existingState;
            }

            var created = new RuntimeGraphNodeState
            {
                RuleId = request.RuleId,
                UserId = request.UserId,
                NodeId = nodeId,
                StateJson = "{}",
                UpdatedAt = DateTime.UtcNow
            };

            if (request.Persist)
            {
                _db.GraphNodeStates.Add(created);
            }

            stateById[nodeId] = created;
            return created;
        }

        var variables = ParseVariables(request.VariablesJson);
        var result = _executor.Execute(request.GraphJson, new RuntimeGraphExecutionContext(
            EventContext: contextDoc.RootElement,
            OccurredAtUtc: request.OccurredAtUtc,
            GetNodeState: GetNodeState),
            request.StartNodeOverride,
            variables);

        Guid? executionId = null;
        if (request.Persist)
        {
            var execution = new RuntimeGraphExecution
            {
                Id = Guid.NewGuid(),
                RuleId = request.RuleId,
                UserId = request.UserId,
                OperationId = request.OperationId,
                Matched = result.Matched,
                PointsDelta = result.PointsDelta,
                ActionsJson = JsonSerializer.Serialize(result.Actions),
                OccurredAt = request.OccurredAtUtc,
                CreatedAt = DateTime.UtcNow
            };

            _db.GraphExecutions.Add(execution);
            await _db.SaveChangesAsync(ct);
            executionId = execution.Id;
        }

        return new RuntimeGraphResult(true, result.Matched, result.PointsDelta, result.AwardNodeIds, result.Actions, result.AudienceSelection, executionId, null);
    }

    private static Dictionary<string, JsonElement> ParseVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return dict ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
