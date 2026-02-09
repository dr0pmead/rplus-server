using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Graph;

public sealed record LoyaltyGraphValidationError(string Code, string Message, string? NodeId = null);

public sealed record LoyaltyGraphValidationResult(bool IsValid, IReadOnlyList<LoyaltyGraphValidationError> Errors);

public sealed class LoyaltyGraphSchemaValidator
{
    private const int MaxNodes = 64;
    private const int MaxEdges = 128;

    private readonly ILoyaltyGraphNodeCatalog _catalog;

    public LoyaltyGraphSchemaValidator(ILoyaltyGraphNodeCatalog catalog)
    {
        _catalog = catalog;
    }

    public async Task<LoyaltyGraphValidationResult> ValidateAsync(string graphJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            return Invalid(new("GRAPH_EMPTY", "GraphJson is empty."));
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(graphJson);
        }
        catch
        {
            return Invalid(new("GRAPH_INVALID_JSON", "GraphJson is not valid JSON."));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid(new("GRAPH_INVALID_ROOT", "GraphJson must be an object."));
            }

            if (!TryGetString(root, "start", out var start) || string.IsNullOrWhiteSpace(start))
            {
                return Invalid(new("GRAPH_START_REQUIRED", "GraphJson.start is required."));
            }

            if (!TryGetArray(root, "nodes", out var nodesArray))
            {
                return Invalid(new("GRAPH_NODES_REQUIRED", "GraphJson.nodes array is required."));
            }

            if (nodesArray.GetArrayLength() > MaxNodes)
            {
                return Invalid(new("GRAPH_TOO_MANY_NODES", $"GraphJson.nodes exceeds {MaxNodes}."));
            }

            var errors = new List<LoyaltyGraphValidationError>();
            var nodes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            var catalogItems = await _catalog.GetItemsAsync(ct);
            var supported = new HashSet<string>(catalogItems.Select(x => x.Type), StringComparer.OrdinalIgnoreCase);

            foreach (var nodeEl in nodesArray.EnumerateArray())
            {
                if (nodeEl.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new("GRAPH_NODE_INVALID", "Each node must be an object."));
                    continue;
                }

                if (!TryGetString(nodeEl, "id", out var nodeId) || string.IsNullOrWhiteSpace(nodeId))
                {
                    errors.Add(new("GRAPH_NODE_ID_REQUIRED", "Node.id is required."));
                    continue;
                }

                if (nodes.ContainsKey(nodeId!))
                {
                    errors.Add(new("GRAPH_NODE_ID_DUPLICATE", $"Duplicate node id '{nodeId}'.", nodeId));
                    continue;
                }

                if (!TryGetString(nodeEl, "type", out var type) || string.IsNullOrWhiteSpace(type))
                {
                    errors.Add(new("GRAPH_NODE_TYPE_REQUIRED", $"Node '{nodeId}' type is required.", nodeId));
                    continue;
                }

                if (!supported.Contains(type!))
                {
                    errors.Add(new("GRAPH_NODE_TYPE_UNSUPPORTED", $"Node '{nodeId}' type '{type}' is not supported.", nodeId));
                    continue;
                }

                nodes[nodeId!] = nodeEl;
                ValidateNode(nodeId!, type!, nodeEl, errors);
            }

            if (!nodes.ContainsKey(start!))
            {
                errors.Add(new("GRAPH_START_INVALID", $"Start node '{start}' does not exist."));
            }
            else if (TryGetString(nodes[start!], "type", out var startType)
                     && !string.Equals(startType, "trigger", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(startType, "start", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new("GRAPH_START_NOT_TRIGGER", "Start node must be of type 'trigger' or 'start'.", start));
            }

            var edges = new List<(string From, string To, bool? When)>();
            if (TryGetArray(root, "edges", out var edgesArray))
            {
                if (edgesArray.GetArrayLength() > MaxEdges)
                {
                    errors.Add(new("GRAPH_TOO_MANY_EDGES", $"GraphJson.edges exceeds {MaxEdges}."));
                }

                var branchCounts = new Dictionary<string, (int TrueCount, int FalseCount)>(StringComparer.OrdinalIgnoreCase);

                foreach (var edgeEl in edgesArray.EnumerateArray())
                {
                    if (edgeEl.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add(new("GRAPH_EDGE_INVALID", "Each edge must be an object."));
                        continue;
                    }

                    if (!TryGetString(edgeEl, "from", out var from) || !TryGetString(edgeEl, "to", out var to))
                    {
                        errors.Add(new("GRAPH_EDGE_MISSING", "Edge requires from and to."));
                        continue;
                    }

                    if (!nodes.ContainsKey(from!))
                    {
                        errors.Add(new("GRAPH_EDGE_FROM_UNKNOWN", $"Edge.from '{from}' does not exist."));
                    }

                    if (!nodes.ContainsKey(to!))
                    {
                        errors.Add(new("GRAPH_EDGE_TO_UNKNOWN", $"Edge.to '{to}' does not exist."));
                    }

                    if (TryGetBoolean(edgeEl, "when", out var when))
                    {
                        if (!branchCounts.TryGetValue(from!, out var counts))
                        {
                            counts = (0, 0);
                        }

                        if (when)
                        {
                            counts.TrueCount += 1;
                        }
                        else
                        {
                            counts.FalseCount += 1;
                        }

                        branchCounts[from!] = counts;
                        edges.Add((from!, to!, when));
                    }
                    else
                    {
                        edges.Add((from!, to!, null));
                    }
                }

                foreach (var (nodeId, counts) in branchCounts)
                {
                    if (counts.TrueCount > 1 || counts.FalseCount > 1)
                    {
                        errors.Add(new("GRAPH_EDGE_BRANCH_DUPLICATE", $"Node '{nodeId}' has duplicate branch edges.", nodeId));
                    }
                }
            }

            // Ensure required outgoing edges.
            var outgoing = edges.GroupBy(x => x.From, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var (nodeId, node) in nodes)
            {
                if (!TryGetString(node, "type", out var type) || string.IsNullOrWhiteSpace(type))
                    continue;

                if (string.Equals(type, "end", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!outgoing.TryGetValue(nodeId, out var outs) || outs.Count == 0)
                {
                    errors.Add(new("GRAPH_NODE_NO_OUTGOING", $"Node '{nodeId}' has no outgoing edges.", nodeId));
                    continue;
                }

                if (IsConditional(type))
                {
                    var hasTrue = outs.Any(x => x.When == true);
                    var hasFalse = outs.Any(x => x.When == false);
                    if (!hasTrue || !hasFalse)
                    {
                        errors.Add(new("GRAPH_CONDITION_BRANCH_REQUIRED", $"Node '{nodeId}' requires both true/false edges.", nodeId));
                    }
                }
            }

            // Detect unreachable nodes (excluding start).
            if (nodes.ContainsKey(start!))
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>();
                visited.Add(start!);
                queue.Enqueue(start!);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!outgoing.TryGetValue(current, out var outs))
                        continue;

                    foreach (var edge in outs)
                    {
                        if (visited.Add(edge.To))
                        {
                            queue.Enqueue(edge.To);
                        }
                    }
                }

                foreach (var nodeId in nodes.Keys)
                {
                    if (!visited.Contains(nodeId))
                    {
                        errors.Add(new("GRAPH_NODE_UNREACHABLE", $"Node '{nodeId}' is not reachable from start.", nodeId));
                    }
                }
            }

            return errors.Count == 0
                ? new LoyaltyGraphValidationResult(true, Array.Empty<LoyaltyGraphValidationError>())
                : new LoyaltyGraphValidationResult(false, errors);
        }
    }

    private static LoyaltyGraphValidationResult Invalid(LoyaltyGraphValidationError error)
        => new(false, new[] { error });

    private void ValidateNode(string nodeId, string type, JsonElement node, List<LoyaltyGraphValidationError> errors)
    {
        if (string.Equals(type, "filter", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetProperty(node, "logic", out var logic) || logic.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                errors.Add(new("GRAPH_FILTER_LOGIC_REQUIRED", "Filter node requires logic object/array.", nodeId));
            }
        }

        if (string.Equals(type, "award", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetNumber(node, "points", out _))
            {
                errors.Add(new("GRAPH_AWARD_POINTS_REQUIRED", "Award node requires points.", nodeId));
            }
        }

        if (string.Equals(type, "counter", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetInt(node, "target", out var target) || target <= 0)
            {
                errors.Add(new("GRAPH_COUNTER_TARGET_REQUIRED", "Counter node requires target > 0.", nodeId));
            }
        }

        if (string.Equals(type, "cooldown", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetInt(node, "seconds", out var seconds) || seconds < 0)
            {
                errors.Add(new("GRAPH_COOLDOWN_SECONDS_REQUIRED", "Cooldown node requires seconds >= 0.", nodeId));
            }
        }

        if (string.Equals(type, "condition", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetArray(node, "branches", out var branches))
            {
                var index = 0;
                foreach (var branch in branches.EnumerateArray())
                {
                    if (branch.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!TryGetProperty(branch, "data", out var data) || data.ValueKind != JsonValueKind.Object)
                    {
                        data = branch;
                    }

                    ValidateConditionConfig(data, nodeId, $"branch_{index}", errors);
                    index++;
                }
            }
            else
            {
                ValidateConditionConfig(node, nodeId, "node", errors);
            }
        }

        if (string.Equals(type, "range_switch", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "source", out var source) || string.IsNullOrWhiteSpace(source))
            {
                errors.Add(new("GRAPH_RANGE_SOURCE_REQUIRED", "Range node requires source.", nodeId));
            }

            var hasMin = TryGetProperty(node, "min", out _);
            var hasMax = TryGetProperty(node, "max", out _);
            if (!hasMin && !hasMax)
            {
                errors.Add(new("GRAPH_RANGE_BOUNDS_REQUIRED", "Range node requires min or max.", nodeId));
            }
        }

        if (string.Equals(type, "equals_switch", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "source", out var source) || string.IsNullOrWhiteSpace(source))
            {
                errors.Add(new("GRAPH_EQUALS_SOURCE_REQUIRED", "Equals node requires source.", nodeId));
            }

            if (!TryGetProperty(node, "value", out _))
            {
                errors.Add(new("GRAPH_EQUALS_VALUE_REQUIRED", "Equals node requires value.", nodeId));
            }
        }

        if (string.Equals(type, "contains_switch", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "source", out var source) || string.IsNullOrWhiteSpace(source))
            {
                errors.Add(new("GRAPH_CONTAINS_SOURCE_REQUIRED", "Contains node requires source.", nodeId));
            }

            if (!TryGetArray(node, "values", out _))
            {
                errors.Add(new("GRAPH_CONTAINS_VALUES_REQUIRED", "Contains node requires values array.", nodeId));
            }
        }

        if (string.Equals(type, "compute_tenure", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "source", out var source) || string.IsNullOrWhiteSpace(source))
            {
                errors.Add(new("GRAPH_TENURE_SOURCE_REQUIRED", "Compute tenure requires source path.", nodeId));
            }
        }

        if (string.Equals(type, "streak_daily", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetProperty(node, "basePoints", out _))
            {
                errors.Add(new("GRAPH_STREAK_BASE_REQUIRED", "Streak node requires basePoints.", nodeId));
            }
        }

        if (string.Equals(type, "var_set", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "key", out var key) || string.IsNullOrWhiteSpace(key))
            {
                errors.Add(new("GRAPH_VAR_KEY_REQUIRED", "Variable set requires key.", nodeId));
            }
        }

        if (string.Equals(type, "state_get", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "key", out var key) || string.IsNullOrWhiteSpace(key))
            {
                errors.Add(new("GRAPH_STATE_KEY_REQUIRED", "State get requires key.", nodeId));
            }

            if (!TryGetString(node, "target", out var target) || string.IsNullOrWhiteSpace(target))
            {
                errors.Add(new("GRAPH_STATE_TARGET_REQUIRED", "State get requires target variable.", nodeId));
            }
        }

        if (string.Equals(type, "state_set", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "key", out var key) || string.IsNullOrWhiteSpace(key))
            {
                errors.Add(new("GRAPH_STATE_KEY_REQUIRED", "State set requires key.", nodeId));
            }
        }

        if (string.Equals(type, "action_notification", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "channel", out var channel) || string.IsNullOrWhiteSpace(channel))
            {
                errors.Add(new("GRAPH_NOTIFICATION_CHANNEL_REQUIRED", "Notification node requires channel.", nodeId));
            }
        }

        if (string.Equals(type, "action_feed_post", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetString(node, "channel", out var channel) || string.IsNullOrWhiteSpace(channel))
            {
                errors.Add(new("GRAPH_FEED_CHANNEL_REQUIRED", "Feed post node requires channel.", nodeId));
            }
        }

        if (string.Equals(type, "action_update_profile", StringComparison.OrdinalIgnoreCase))
        {
            var hasLevel = TryGetString(node, "setLevel", out var level) && !string.IsNullOrWhiteSpace(level);
            if (!hasLevel)
            {
                hasLevel = TryGetString(node, "level", out var levelAlt) && !string.IsNullOrWhiteSpace(levelAlt);
            }

            var tags = ReadStringArray(node, "addTags");
            if (tags.Length == 0)
            {
                tags = ReadStringArray(node, "tagsAdd");
            }

            if (!hasLevel && tags.Length == 0)
            {
                errors.Add(new("GRAPH_UPDATE_PROFILE_EMPTY", "Update profile node requires setLevel or addTags.", nodeId));
            }
        }
    }

    private static bool IsConditional(string type)
        => string.Equals(type, "filter", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "counter", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "cooldown", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "condition", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "range_switch", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "equals_switch", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "contains_switch", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "streak_daily", StringComparison.OrdinalIgnoreCase);

    private static void ValidateConditionConfig(JsonElement node, string nodeId, string context, List<LoyaltyGraphValidationError> errors)
    {
        if (!TryGetString(node, "source", out var source) || string.IsNullOrWhiteSpace(source))
        {
            errors.Add(new("GRAPH_CONDITION_SOURCE_REQUIRED", $"Condition {context} requires source.", nodeId));
        }

        var mode = string.Empty;
        if (TryGetString(node, "mode", out var modeRaw) && !string.IsNullOrWhiteSpace(modeRaw))
        {
            mode = modeRaw.Trim().ToLowerInvariant();
        }
        else if (TryGetProperty(node, "min", out _) || TryGetProperty(node, "max", out _))
        {
            mode = "range";
        }
        else if (TryGetArray(node, "values", out _) || (TryGetString(node, "values", out var valuesRaw) && !string.IsNullOrWhiteSpace(valuesRaw)))
        {
            mode = "contains";
        }
        else if (TryGetProperty(node, "value", out _))
        {
            mode = "equals";
        }

        if (string.IsNullOrWhiteSpace(mode) || (mode != "range" && mode != "equals" && mode != "contains"))
        {
            errors.Add(new("GRAPH_CONDITION_MODE_REQUIRED", $"Condition {context} requires mode: range|equals|contains.", nodeId));
        }
        else if (mode == "range")
        {
            var hasMin = TryGetProperty(node, "min", out _);
            var hasMax = TryGetProperty(node, "max", out _);
            if (!hasMin && !hasMax)
            {
                errors.Add(new("GRAPH_CONDITION_RANGE_REQUIRED", $"Condition {context} range requires min or max.", nodeId));
            }
        }
        else if (mode == "equals")
        {
            if (!TryGetProperty(node, "value", out _))
            {
                errors.Add(new("GRAPH_CONDITION_VALUE_REQUIRED", $"Condition {context} equals requires value.", nodeId));
            }
        }
        else if (mode == "contains")
        {
            if (!TryGetArray(node, "values", out _) && !(TryGetString(node, "values", out var valuesRaw) && !string.IsNullOrWhiteSpace(valuesRaw)))
            {
                errors.Add(new("GRAPH_CONDITION_VALUES_REQUIRED", $"Condition {context} contains requires values list.", nodeId));
            }
        }
    }

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
        => obj.TryGetProperty(name, out value);

    private static string[] ReadStringArray(JsonElement obj, string name)
    {
        if (!TryGetProperty(obj, name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value!);
                }
            }
        }

        return list.ToArray();
    }
}
