using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Graph;

public sealed record LoyaltyGraphNodeTemplate(
    string Type,
    string Category,
    string Label,
    string Description,
    IReadOnlyList<string> Outputs,
    IReadOnlyDictionary<string, string> RequiredProps,
    IReadOnlyList<string> Contexts,
    int Version = 1,
    bool Deprecated = false,
    bool Advanced = false);

public sealed class LoyaltyGraphNodeCatalog : ILoyaltyGraphNodeCatalog
{
    private readonly IReadOnlyList<LoyaltyGraphNodeTemplate> _items;
    private readonly Dictionary<string, LoyaltyGraphNodeTemplate> _byType;

    public LoyaltyGraphNodeCatalog()
    {
        _items = new List<LoyaltyGraphNodeTemplate>
        {
            new(
                Type: "trigger",
                Category: "trigger",
                Label: "Trigger",
                Description: "Entry point for a loyalty event.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string>(),
                Contexts: ["all"]),
            new(
                Type: "filter",
                Category: "condition",
                Label: "Condition",
                Description: "Evaluate JsonLogic against event context.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["logic"] = "object|array" },
                Contexts: ["all"]),
            new(
                Type: "counter",
                Category: "condition",
                Label: "Counter",
                Description: "Count occurrences before passing.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["target"] = "int" },
                Contexts: ["all"]),
            new(
                Type: "cooldown",
                Category: "condition",
                Label: "Cooldown",
                Description: "Allow passing only after cooldown period.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["seconds"] = "int" },
                Contexts: ["all"]),
            new(
                Type: "condition",
                Category: "condition",
                Label: "Condition",
                Description: "Universal condition with mode (equals/range/contains).",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["mode"] = "string", ["source"] = "string" },
                Contexts: ["all"]),
            new(
                Type: "range_switch",
                Category: "condition",
                Label: "Range Check",
                Description: "Pass if value is within range.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["source"] = "string", ["min"] = "number", ["max"] = "number?" },
                Contexts: ["all"],
                Version: 3,
                Deprecated: true),
            new(
                Type: "equals_switch",
                Category: "condition",
                Label: "Equals",
                Description: "Compare source with value.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["source"] = "string", ["value"] = "any" },
                Contexts: ["all"],
                Version: 3,
                Deprecated: true),
            new(
                Type: "contains_switch",
                Category: "condition",
                Label: "Contains (List)",
                Description: "Check if source is in values list.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["source"] = "string", ["values"] = "array" },
                Contexts: ["all"],
                Version: 3,
                Deprecated: true),
            new(
                Type: "compute_tenure",
                Category: "data",
                Label: "Compute Tenure",
                Description: "Compute tenure from a date and store in variable.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["source"] = "string", ["target"] = "string?" },
                Contexts: ["all"]),
            new(
                Type: "streak_daily",
                Category: "condition",
                Label: "Daily Streak",
                Description: "Track daily streak and compute bonus.",
                Outputs: ["true", "false"],
                RequiredProps: new Dictionary<string, string> { ["basePoints"] = "number", ["stepPoints"] = "number?", ["maxPoints"] = "number?" },
                Contexts: ["all"]),
            new(
                Type: "var_set",
                Category: "data",
                Label: "Set Variable",
                Description: "Set variable from value or path.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["key"] = "string", ["value"] = "any?" },
                Contexts: ["all"]),
            new(
                Type: "state_get",
                Category: "data",
                Label: "Get State",
                Description: "Load node state value into variable.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["key"] = "string", ["target"] = "string" },
                Contexts: ["all"]),
            new(
                Type: "state_set",
                Category: "data",
                Label: "Set State",
                Description: "Persist value into node state.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["key"] = "string" },
                Contexts: ["all"]),
            new(
                Type: "audience_selector",
                Category: "audience",
                Label: "Audience Selector",
                Description: "Select audience before per-user execution.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["entity"] = "string", ["limit"] = "int?" },
                Contexts: ["loyalty"]),
            new(
                Type: "award",
                Category: "action",
                Label: "Award Points",
                Description: "Add points to user balance.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["points"] = "number" },
                Contexts: ["loyalty"]),
            new(
                Type: "action_update_profile",
                Category: "action",
                Label: "Update Profile",
                Description: "Set level or add tags.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["setLevel"] = "string?", ["addTags"] = "array?" },
                Contexts: ["loyalty"]),
            new(
                Type: "action_notification",
                Category: "action",
                Label: "Notification",
                Description: "Send user notification.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["channel"] = "string" },
                Contexts: ["loyalty"]),
            new(
                Type: "action_feed_post",
                Category: "action",
                Label: "Feed Post",
                Description: "Publish feed message.",
                Outputs: ["next"],
                RequiredProps: new Dictionary<string, string> { ["channel"] = "string" },
                Contexts: ["loyalty"]),
            new(
                Type: "end",
                Category: "flow",
                Label: "End",
                Description: "Terminate graph execution.",
                Outputs: Array.Empty<string>(),
                RequiredProps: new Dictionary<string, string>(),
                Contexts: ["all"])
        };

        _byType = _items.ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<LoyaltyGraphNodeTemplate> Items => _items;

    public Task<IReadOnlyList<LoyaltyGraphNodeTemplate>> GetItemsAsync(CancellationToken ct = default)
        => Task.FromResult(_items);

    public bool IsSupported(string type) => _byType.ContainsKey(type ?? string.Empty);

    public LoyaltyGraphNodeTemplate? Get(string type)
        => _byType.TryGetValue(type ?? string.Empty, out var item) ? item : null;
}
