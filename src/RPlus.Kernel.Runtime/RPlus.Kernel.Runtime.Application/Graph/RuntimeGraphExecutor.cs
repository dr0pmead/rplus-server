using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using RPlus.Kernel.Runtime.Domain.Entities;

namespace RPlus.Kernel.Runtime.Application.Graph;

public sealed record RuntimeGraphExecutionResult(
    bool Matched,
    decimal PointsDelta,
    IReadOnlyList<string> AwardNodeIds,
    IReadOnlyList<RuntimeGraphAction> Actions,
    RuntimeGraphAudienceSelection? AudienceSelection);

public sealed record RuntimeGraphAction(
    string NodeId,
    string Kind,
    string DataJson);

public sealed record RuntimeGraphAudienceSelection(
    string NodeId,
    string QueryJson,
    string ResumeFromNodeId);

public sealed record RuntimeGraphExecutionContext(
    JsonElement EventContext,
    DateTime OccurredAtUtc,
    Func<string, RuntimeGraphNodeState> GetNodeState);

/// <summary>
/// Executes stored JSON graphs against an incoming JSON event context.
/// Current v1 supports: trigger -> filter (JsonLogic) -> award(points).
/// </summary>
public sealed class RuntimeGraphExecutor
{
    private const int MaxNodes = 64;
    private const int MaxEdges = 128;
    private const int MaxSteps = 128;

    public RuntimeGraphExecutionResult Execute(
        string graphJson,
        RuntimeGraphExecutionContext context,
        string? startNodeOverride = null,
        IReadOnlyDictionary<string, JsonElement>? initialVariables = null)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        using var doc = JsonDocument.Parse(graphJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        if (!TryGetString(root, "start", out var start) || string.IsNullOrWhiteSpace(start))
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        if (!TryGetArray(root, "nodes", out var nodesArray))
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        if (nodesArray.GetArrayLength() > MaxNodes)
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        var nodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var nodeEl in nodesArray.EnumerateArray())
        {
            if (nodeEl.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryGetString(nodeEl, "id", out var id) || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!TryGetString(nodeEl, "type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var node = new GraphNode(id.Trim(), type.Trim(), nodeEl);
            nodes[node.Id] = node;
        }

        if (!nodes.ContainsKey(start))
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        var edges = new List<GraphEdge>();
        if (TryGetArray(root, "edges", out var edgesArray))
        {
            if (edgesArray.GetArrayLength() > MaxEdges)
            {
                return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
            }

            foreach (var edgeEl in edgesArray.EnumerateArray())
            {
                if (edgeEl.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetString(edgeEl, "from", out var from) || !TryGetString(edgeEl, "to", out var to))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    continue;
                }

                bool? when = null;
                if (TryGetBoolean(edgeEl, "when", out var whenBool))
                {
                    when = whenBool;
                }

                edges.Add(new GraphEdge(from!, to!, when));
            }
        }

        var outgoing = edges
            .Where(e => !string.IsNullOrWhiteSpace(e.From) && !string.IsNullOrWhiteSpace(e.To))
            .GroupBy(e => e.From.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var awardNodeIds = new List<string>();
        var actions = new List<RuntimeGraphAction>();
        RuntimeGraphAudienceSelection? audienceSelection = null;
        decimal points = 0;
        var matched = !string.IsNullOrWhiteSpace(startNodeOverride);
        var variables = initialVariables == null
            ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>(initialVariables, StringComparer.OrdinalIgnoreCase);

        var current = string.IsNullOrWhiteSpace(startNodeOverride) ? start : startNodeOverride!.Trim();
        if (!nodes.ContainsKey(current))
        {
            return new RuntimeGraphExecutionResult(false, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), null);
        }

        for (var step = 0; step < MaxSteps; step++)
        {
            if (!nodes.TryGetValue(current, out var node))
            {
                break;
            }

            var type = node.Type;
            if (type.Equals("trigger", StringComparison.OrdinalIgnoreCase)
                || type.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("audience_selector", StringComparison.OrdinalIgnoreCase))
            {
                var resume = NextNode(outgoing, current, branch: null);
                if (string.IsNullOrWhiteSpace(resume))
                {
                    audienceSelection = new RuntimeGraphAudienceSelection(node.Id, QueryJson: "{}", ResumeFromNodeId: string.Empty);
                    break;
                }

                var queryJson = "{}";
                if (TryGetProperty(node.Raw, "query", out var queryEl) && queryEl.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    queryJson = queryEl.GetRawText();
                }

                audienceSelection = new RuntimeGraphAudienceSelection(node.Id, QueryJson: queryJson, ResumeFromNodeId: resume!.Trim());
                break;
            }

            if (type.Equals("filter", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetProperty(node.Raw, "logic", out var logic))
                {
                    break;
                }

                var passed = JsonLogicEvaluator.EvaluateBoolean(logic, context.EventContext);
                current = NextNode(outgoing, current, branch: passed);
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("var_set", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "key", out var key) || string.IsNullOrWhiteSpace(key))
                {
                    break;
                }

                if (TryGetProperty(node.Raw, "value", out var valueEl))
                {
                    variables[key!] = valueEl.Clone();
                }
                else if (TryGetString(node.Raw, "source", out var source) && TryResolveSource(source!, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var resolved))
                {
                    variables[key!] = resolved;
                }

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("state_get", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "key", out var key) || string.IsNullOrWhiteSpace(key)
                    || !TryGetString(node.Raw, "target", out var target) || string.IsNullOrWhiteSpace(target))
                {
                    break;
                }

                var state = context.GetNodeState(node.Id);
                var dict = ParseState(state.StateJson);
                if (dict.TryGetValue(key!, out var stateValue))
                {
                    variables[target!] = stateValue;
                }

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("state_set", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "key", out var key) || string.IsNullOrWhiteSpace(key))
                {
                    break;
                }

                var state = context.GetNodeState(node.Id);
                var dict = ParseState(state.StateJson);
                if (TryGetProperty(node.Raw, "value", out var valueEl))
                {
                    dict[key!] = valueEl.Clone();
                }
                else if (TryGetString(node.Raw, "source", out var source) && TryResolveSource(source!, context, variables, dict, out var resolved))
                {
                    dict[key!] = resolved;
                }

                dict["updatedAt"] = JsonString(context.OccurredAtUtc.ToString("O"));
                state.StateJson = JsonSerializer.Serialize(dict);
                state.UpdatedAt = DateTime.UtcNow;

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("counter", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetInt(node.Raw, "target", out var target) || target <= 0)
                {
                    break;
                }

                var behavior = "loop";
                if (TryGetString(node.Raw, "behavior", out var behaviorRaw) && !string.IsNullOrWhiteSpace(behaviorRaw))
                {
                    behavior = behaviorRaw.Trim();
                }

                // Backward compatibility for older graphs.
                // resetOnTarget:true => loop, resetOnTarget:false => cap/once.
                if (TryGetBoolean(node.Raw, "resetOnTarget", out var rot) && string.Equals(behavior, "loop", StringComparison.OrdinalIgnoreCase))
                {
                    behavior = rot ? "loop" : "cap";
                }

                var state = context.GetNodeState(node.Id);
                var dict = ParseState(state.StateJson);

                var count = GetInt(dict, "count", 0);
                var completed = GetBool(dict, "completed", defaultValue: false);

                bool passed;
                if (completed && (string.Equals(behavior, "cap", StringComparison.OrdinalIgnoreCase) || string.Equals(behavior, "once", StringComparison.OrdinalIgnoreCase)))
                {
                    passed = false;
                }
                else
                {
                    count++;
                    passed = count >= target;
                }

                if (passed && string.Equals(behavior, "loop", StringComparison.OrdinalIgnoreCase))
                {
                    count = 0;
                }

                if (passed && (string.Equals(behavior, "cap", StringComparison.OrdinalIgnoreCase) || string.Equals(behavior, "once", StringComparison.OrdinalIgnoreCase)))
                {
                    completed = true;
                    count = Math.Max(count, target);
                    dict["completed"] = JsonBool(true);
                    dict["completedAt"] = JsonString(context.OccurredAtUtc.ToString("O"));
                }

                dict["count"] = JsonNumber(count);
                dict["updatedAt"] = JsonString(context.OccurredAtUtc.ToString("O"));
                state.StateJson = JsonSerializer.Serialize(dict);
                state.UpdatedAt = DateTime.UtcNow;

                current = NextNode(outgoing, current, branch: passed) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("range_switch", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "source", out var source) || string.IsNullOrWhiteSpace(source))
                {
                    break;
                }

                var state = context.GetNodeState(node.Id);
                var dict = ParseState(state.StateJson);
                if (!TryResolveNumber(source!, context, variables, dict, out var value))
                {
                    current = NextNode(outgoing, current, branch: false) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        break;
                    }

                    continue;
                }

                var hasMin = TryGetProperty(node.Raw, "min", out var minEl);
                var hasMax = TryGetProperty(node.Raw, "max", out var maxEl);
                var min = 0m;
                var max = 0m;
                if (hasMin)
                {
                    if (minEl.ValueKind == JsonValueKind.Number && minEl.TryGetDecimal(out var minNumber))
                    {
                        min = minNumber;
                    }
                    else if (minEl.ValueKind == JsonValueKind.String && TryResolveNumber(minEl.GetString() ?? string.Empty, context, variables, dict, out var minResolved))
                    {
                        min = minResolved;
                    }
                    else
                    {
                        hasMin = false;
                    }
                }

                if (hasMax)
                {
                    if (maxEl.ValueKind == JsonValueKind.Number && maxEl.TryGetDecimal(out var maxNumber))
                    {
                        max = maxNumber;
                    }
                    else if (maxEl.ValueKind == JsonValueKind.String && TryResolveNumber(maxEl.GetString() ?? string.Empty, context, variables, dict, out var maxResolved))
                    {
                        max = maxResolved;
                    }
                    else
                    {
                        hasMax = false;
                    }
                }
                var inclusive = true;
                if (TryGetBoolean(node.Raw, "inclusive", out var inc))
                {
                    inclusive = inc;
                }

                var pass = true;
                if (hasMin)
                {
                    pass = inclusive ? value >= min : value > min;
                }

                if (pass && hasMax)
                {
                    pass = inclusive ? value <= max : value < max;
                }

                current = NextNode(outgoing, current, branch: pass) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("equals_switch", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "source", out var source) || string.IsNullOrWhiteSpace(source))
                {
                    break;
                }

                if (!TryResolveSource(source!, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var left))
                {
                    current = NextNode(outgoing, current, branch: false) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        break;
                    }

                    continue;
                }

                JsonElement right;
                if (TryGetProperty(node.Raw, "value", out var valueEl))
                {
                    if (valueEl.ValueKind == JsonValueKind.String
                        && TryResolveSource(valueEl.GetString() ?? string.Empty, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var resolved))
                    {
                        right = resolved;
                    }
                    else
                    {
                        right = valueEl;
                    }
                }
                else
                {
                    current = NextNode(outgoing, current, branch: false) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        break;
                    }

                    continue;
                }

                var pass = JsonEquals(left, right);
                current = NextNode(outgoing, current, branch: pass) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("contains_switch", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "source", out var source) || string.IsNullOrWhiteSpace(source))
                {
                    break;
                }

                if (!TryResolveSource(source!, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var left))
                {
                    current = NextNode(outgoing, current, branch: false) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        break;
                    }

                    continue;
                }

                if (!TryGetArray(node.Raw, "values", out var valuesEl))
                {
                    current = NextNode(outgoing, current, branch: false) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        break;
                    }

                    continue;
                }

                var pass = false;
                foreach (var item in valuesEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String
                        && TryResolveSource(item.GetString() ?? string.Empty, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var resolved))
                    {
                        if (JsonEquals(left, resolved))
                        {
                            pass = true;
                            break;
                        }
                        continue;
                    }

                    if (JsonEquals(left, item))
                    {
                        pass = true;
                        break;
                    }
                }

                current = NextNode(outgoing, current, branch: pass) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("compute_tenure", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "source", out var source) || string.IsNullOrWhiteSpace(source))
                {
                    break;
                }

                var target = "tenureYears";
                if (TryGetString(node.Raw, "target", out var targetRaw) && !string.IsNullOrWhiteSpace(targetRaw))
                {
                    target = targetRaw.Trim();
                }

                var unit = "years";
                if (TryGetString(node.Raw, "unit", out var unitRaw) && !string.IsNullOrWhiteSpace(unitRaw))
                {
                    unit = unitRaw.Trim().ToLowerInvariant();
                }

                if (TryResolveDate(source!, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var date))
                {
                    var span = context.OccurredAtUtc - date;
                    var value = unit switch
                    {
                        "days" => Math.Floor(span.TotalDays),
                        "months" => Math.Floor(span.TotalDays / 30.0),
                        _ => Math.Floor(span.TotalDays / 365.25)
                    };
                    variables[target] = JsonNumber((int)Math.Max(0, value));
                }

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("cooldown", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetInt(node.Raw, "seconds", out var seconds) || seconds < 0)
                {
                    break;
                }

                var setOnPass = true;
                if (TryGetBoolean(node.Raw, "setOnPass", out var sop))
                {
                    setOnPass = sop;
                }

                var state = context.GetNodeState(node.Id);
                var dict = ParseState(state.StateJson);

                var lastAt = GetDateTime(dict, "lastAt");
                var passed = lastAt == null || (context.OccurredAtUtc - lastAt.Value) >= TimeSpan.FromSeconds(seconds);

                if (passed && setOnPass)
                {
                    dict["lastAt"] = JsonString(context.OccurredAtUtc.ToString("O"));
                    dict["updatedAt"] = JsonString(context.OccurredAtUtc.ToString("O"));
                    state.StateJson = JsonSerializer.Serialize(dict);
                    state.UpdatedAt = DateTime.UtcNow;
                }

                current = NextNode(outgoing, current, branch: passed) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("streak_daily", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveNumberFromNode(node.Raw, "basePoints", context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var basePoints))
                {
                    break;
                }

                TryResolveNumberFromNode(node.Raw, "stepPoints", context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var stepPoints);
                TryResolveNumberFromNode(node.Raw, "maxPoints", context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var maxPoints);

                var varStreak = "streakCount";
                var varBonus = "streakBonus";
                if (TryGetString(node.Raw, "varStreak", out var varStreakRaw) && !string.IsNullOrWhiteSpace(varStreakRaw))
                {
                    varStreak = varStreakRaw.Trim();
                }

                if (TryGetString(node.Raw, "varBonus", out var varBonusRaw) && !string.IsNullOrWhiteSpace(varBonusRaw))
                {
                    varBonus = varBonusRaw.Trim();
                }

                var state = context.GetNodeState(node.Id);
                var dict = ParseState(state.StateJson);
                var lastAt = GetDateTime(dict, "lastDate");

                var today = DateOnly.FromDateTime(context.OccurredAtUtc);
                var passed = false;
                var streak = GetInt(dict, "streak", 0);

                if (lastAt.HasValue)
                {
                    var lastDay = DateOnly.FromDateTime(lastAt.Value);
                    if (lastDay == today)
                    {
                        passed = false;
                    }
                    else if (lastDay == today.AddDays(-1))
                    {
                        streak += 1;
                        passed = true;
                    }
                    else
                    {
                        streak = 1;
                        passed = true;
                    }
                }
                else
                {
                    streak = 1;
                    passed = true;
                }

                if (passed)
                {
                    var bonus = basePoints + ((streak - 1) * stepPoints);
                    if (TryGetProperty(node.Raw, "maxPoints", out _))
                    {
                        bonus = Math.Min(bonus, maxPoints);
                    }

                    variables[varStreak] = JsonNumber(streak);
                    variables[varBonus] = JsonNumber((int)Math.Round(bonus, MidpointRounding.AwayFromZero));

                    dict["streak"] = JsonNumber(streak);
                    dict["lastDate"] = JsonString(context.OccurredAtUtc.Date.ToString("O"));
                    dict["updatedAt"] = JsonString(context.OccurredAtUtc.ToString("O"));
                    state.StateJson = JsonSerializer.Serialize(dict);
                    state.UpdatedAt = DateTime.UtcNow;
                }

                current = NextNode(outgoing, current, branch: passed) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("award", StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetString(node.Raw, "pointsVar", out var pointsVar) && !string.IsNullOrWhiteSpace(pointsVar)
                    && TryResolveNumber($"var:{pointsVar}", context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var varPoints))
                {
                    points += varPoints;
                    awardNodeIds.Add(node.Id);
                }
                else if (TryGetNumber(node.Raw, "points", out var pts))
                {
                    points += pts;
                    awardNodeIds.Add(node.Id);
                }

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("action_update_profile", StringComparison.OrdinalIgnoreCase))
            {
                var setLevel = TryGetString(node.Raw, "setLevel", out var level) ? level : null;
                if (string.IsNullOrWhiteSpace(setLevel) && TryGetString(node.Raw, "level", out var level2))
                {
                    setLevel = level2;
                }

                if (!string.IsNullOrWhiteSpace(setLevel) && setLevel.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryResolveSource(setLevel, context, variables, ParseState(context.GetNodeState(node.Id).StateJson), out var resolved)
                        && resolved.ValueKind == JsonValueKind.String)
                    {
                        setLevel = resolved.GetString();
                    }
                }

                var tags = ReadStringArray(node.Raw, "addTags");
                if (tags.Length == 0)
                {
                    tags = ReadStringArray(node.Raw, "tagsAdd");
                }

                var dataJson = JsonSerializer.Serialize(new
                {
                    setLevel = string.IsNullOrWhiteSpace(setLevel) ? null : setLevel.Trim(),
                    addTags = tags
                });
                actions.Add(new RuntimeGraphAction(node.Id, "update_profile", dataJson));

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("action_notification", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "channel", out var channel) || string.IsNullOrWhiteSpace(channel))
                {
                    break;
                }

                TryGetString(node.Raw, "title", out var title);
                TryGetString(node.Raw, "body", out var body);

                var dataJson = JsonSerializer.Serialize(new
                {
                    channel = channel!.Trim(),
                    title = title ?? string.Empty,
                    body = body ?? string.Empty
                });
                actions.Add(new RuntimeGraphAction(node.Id, "notification", dataJson));

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("action_feed_post", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetString(node.Raw, "channel", out var channel) || string.IsNullOrWhiteSpace(channel))
                {
                    break;
                }

                TryGetString(node.Raw, "content", out var content);

                var dataJson = JsonSerializer.Serialize(new
                {
                    channel = channel!.Trim(),
                    content = content ?? string.Empty
                });
                actions.Add(new RuntimeGraphAction(node.Id, "feed_post", dataJson));

                current = NextNode(outgoing, current, branch: null) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                {
                    break;
                }

                continue;
            }

            if (type.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            break;
        }

        // For audience selection graphs, the actual per-user effects are evaluated after the selector; consider it matched if we reached a trigger.
        if (audienceSelection != null)
        {
            return new RuntimeGraphExecutionResult(matched, 0, Array.Empty<string>(), Array.Empty<RuntimeGraphAction>(), audienceSelection);
        }

        var hasEffects = points > 0 || actions.Count > 0;
        return new RuntimeGraphExecutionResult(matched && hasEffects, points, awardNodeIds, actions, null);
    }

    private static string? NextNode(IReadOnlyDictionary<string, List<GraphEdge>> outgoing, string from, bool? branch)
    {
        if (!outgoing.TryGetValue(from, out var edges) || edges.Count == 0)
        {
            return null;
        }

        if (branch is not null)
        {
            var match = edges.FirstOrDefault(e => e.When == branch);
            if (match != null)
            {
                return match.To.Trim();
            }
        }

        return edges.FirstOrDefault(e => e.When is null)?.To.Trim()
               ?? edges.First().To.Trim();
    }

    private sealed record GraphNode(string Id, string Type, JsonElement Raw);

    private sealed record GraphEdge(string From, string To, bool? When);

    private static bool TryGetArray(JsonElement obj, string name, out JsonElement value)
        => TryGetProperty(obj, name, out value) && value.ValueKind == JsonValueKind.Array;

    private static bool TryGetString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (!TryGetProperty(obj, name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = el.GetString();
        return true;
    }

    private static bool TryGetBoolean(JsonElement obj, string name, out bool value)
    {
        value = default;
        if (!TryGetProperty(obj, name, out var el) || (el.ValueKind != JsonValueKind.True && el.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        value = el.ValueKind == JsonValueKind.True;
        return true;
    }

    private static bool TryGetNumber(JsonElement obj, string name, out decimal value)
    {
        value = default;
        if (!TryGetProperty(obj, name, out var el) || el.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (el.TryGetDecimal(out value))
        {
            return true;
        }

        if (el.TryGetDouble(out var d))
        {
            value = (decimal)d;
            return true;
        }

        return decimal.TryParse(el.GetRawText(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetInt(JsonElement obj, string name, out int value)
    {
        value = default;
        if (!TryGetProperty(obj, name, out var el))
        {
            return false;
        }

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value))
        {
            return true;
        }

        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string[] ReadStringArray(JsonElement obj, string name)
    {
        if (!TryGetProperty(obj, name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var v = item.GetString();
            if (string.IsNullOrWhiteSpace(v))
            {
                continue;
            }

            list.Add(v.Trim());
        }

        if (list.Count == 0)
        {
            return Array.Empty<string>();
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Dictionary<string, JsonElement> ParseState(string? json)
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

    private static int GetInt(Dictionary<string, JsonElement> dict, string key, int defaultValue)
    {
        if (!dict.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i))
        {
            return i;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out i))
        {
            return i;
        }

        return defaultValue;
    }

    private static bool GetBool(Dictionary<string, JsonElement> dict, string key, bool defaultValue)
    {
        if (!dict.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var b)) return b;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)) return i != 0;
        return defaultValue;
    }

    private static DateTime? GetDateTime(Dictionary<string, JsonElement> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (DateTime.TryParse(value.GetString(), null, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
        }

        return null;
    }

    private static JsonElement JsonNumber(int value)
    {
        using var d = JsonDocument.Parse(value.ToString(CultureInfo.InvariantCulture));
        return d.RootElement.Clone();
    }

    private static JsonElement JsonString(string value)
    {
        using var d = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return d.RootElement.Clone();
    }

    private static JsonElement JsonBool(bool value)
    {
        using var d = JsonDocument.Parse(value ? "true" : "false");
        return d.RootElement.Clone();
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            return left.GetRawText() == right.GetRawText();
        }

        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            return string.Equals(left.GetString(), right.GetString(), StringComparison.OrdinalIgnoreCase);
        }

        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False && right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return left.ValueKind == right.ValueKind;
        }

        return left.GetRawText() == right.GetRawText();
    }

    private static bool TryResolveSource(string source, RuntimeGraphExecutionContext context, Dictionary<string, JsonElement> vars, Dictionary<string, JsonElement> state, out JsonElement value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var trimmed = source.Trim();
        if (trimmed.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
        {
            var key = trimmed[4..].Trim();
            return vars.TryGetValue(key, out value);
        }

        if (trimmed.StartsWith("state:", StringComparison.OrdinalIgnoreCase))
        {
            var key = trimmed[6..].Trim();
            return state.TryGetValue(key, out value);
        }

        if (trimmed.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].Trim();
        }

        return TryGetPathValue(context.EventContext, trimmed, out value);
    }

    private static bool TryResolveNumber(string source, RuntimeGraphExecutionContext context, Dictionary<string, JsonElement> vars, Dictionary<string, JsonElement> state, out decimal value)
    {
        value = default;
        if (!TryResolveSource(source, context, vars, state, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveNumberFromNode(JsonElement node, string name, RuntimeGraphExecutionContext context, Dictionary<string, JsonElement> vars, Dictionary<string, JsonElement> state, out decimal value)
    {
        value = default;
        if (!TryGetProperty(node, name, out var el))
        {
            return false;
        }

        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var number))
        {
            value = number;
            return true;
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            return TryResolveNumber(el.GetString() ?? string.Empty, context, vars, state, out value);
        }

        return false;
    }

    private static bool TryResolveDate(string source, RuntimeGraphExecutionContext context, Dictionary<string, JsonElement> vars, Dictionary<string, JsonElement> state, out DateTime value)
    {
        value = default;
        if (!TryResolveSource(source, context, vars, state, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), null, DateTimeStyles.RoundtripKind, out var dt))
        {
            value = dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unix))
        {
            value = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            return true;
        }

        return false;
    }

    private static bool TryGetPathValue(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                value = default;
                return false;
            }

            current = next;
        }

        value = current;
        return true;
    }
}

