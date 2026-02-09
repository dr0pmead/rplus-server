using System;
using System.Collections.Generic;
using System.Text.Json;
using RPlus.SDK.Loyalty.Models;

#nullable enable
namespace RPlus.Loyalty.Domain.Entities;

/// <summary>
/// Extended rule definition persisted by the Loyalty service.
/// Inherits from the SDK model so responses remain compatible with external callers.
/// </summary>
public class LoyaltyRule : RPlus.SDK.Loyalty.Models.LoyaltyRule
{
    /// <summary>
    /// Event type that should trigger this rule (e.g. users.login.streak).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Points delta applied when the rule matches.
    /// </summary>
    public decimal Points { get; set; }

    /// <summary>
    /// JSON blob describing metadata filters (key/value equality).
    /// </summary>
    public string? MetadataFilter { get; set; }

    /// <summary>
    /// Optional explanation displayed in the admin interface.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Rule engine type. When empty, treated as <c>simple_points</c>.
    /// Supported: <c>simple_points</c>, <c>streak_days</c>, <c>count_within_window</c>.
    /// </summary>
    public string? RuleType { get; set; }

    /// <summary>
    /// Optional JSON configuration for the selected <see cref="RuleType"/>.
    /// Stored as JSONB in Postgres.
    /// </summary>
    public string? RuleConfigJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool Matches(IReadOnlyDictionary<string, string>? metadata)
    {
        if (string.IsNullOrWhiteSpace(MetadataFilter))
        {
            return true;
        }

        if (metadata == null || metadata.Count == 0)
        {
            return false;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataFilter);
            if (doc == null || doc.Count == 0)
            {
                return true;
            }

            foreach (var kvp in doc)
            {
                if (!metadata.TryGetValue(kvp.Key, out var value) || !string.Equals(value, kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            // Invalid JSON should not break processing; treat rule as non-match.
            return false;
        }
    }
}
