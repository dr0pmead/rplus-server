using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RPlus.Loyalty.Domain.Entities;

namespace RPlus.Loyalty.Persistence;

public static class LoyaltySystemRuleSeeder
{
    public static async Task ApplyAsync(LoyaltyDbContext db, CancellationToken ct = default)
    {
        await EnsureTenureRuleAsync(db, ct);
        await EnsureDailyStreakRuleAsync(db, ct);
    }

    private static async Task EnsureTenureRuleAsync(LoyaltyDbContext db, CancellationToken ct)
    {
        const string systemKey = "system.loyalty.tenure.level";
        var existing = await db.GraphRules.FirstOrDefaultAsync(r => r.SystemKey == systemKey, ct);

        var graphJson =
            """
            {
              "start": "audience",
              "nodes": [
                { "id": "audience", "type": "audience_selector", "query": {} },
                { "id": "gold_check", "type": "range_switch", "source": "path:user.TenureYears", "min": "var:goldMin" },
                { "id": "gold_set", "type": "action_update_profile", "setLevel": "var:goldLevel" },
                { "id": "silver_check", "type": "range_switch", "source": "path:user.TenureYears", "min": "var:silverMin" },
                { "id": "silver_set", "type": "action_update_profile", "setLevel": "var:silverLevel" },
                { "id": "bronze_set", "type": "action_update_profile", "setLevel": "var:bronzeLevel" },
                { "id": "end", "type": "end" }
              ],
              "edges": [
                { "from": "audience", "to": "gold_check" },
                { "from": "gold_check", "to": "gold_set", "when": true },
                { "from": "gold_check", "to": "silver_check", "when": false },
                { "from": "silver_check", "to": "silver_set", "when": true },
                { "from": "silver_check", "to": "bronze_set", "when": false },
                { "from": "gold_set", "to": "end" },
                { "from": "silver_set", "to": "end" },
                { "from": "bronze_set", "to": "end" }
              ]
            }
            """;

        var variablesJson =
            """
            {
              "goldMin": 5,
              "silverMin": 3,
              "goldLevel": "Gold",
              "silverLevel": "Silver",
              "bronzeLevel": "Bronze",
              "schedule": {
                "kind": "daily",
                "time": "00:05",
                "utcOffsetMinutes": 300
              }
            }
            """;

        if (existing == null)
        {
            var rule = new LoyaltyGraphRule
            {
                Id = Guid.NewGuid(),
                Name = "System: Tenure Level Update",
                Topic = "system.cron.v1",
                Priority = 100,
                IsActive = true,
                IsSystem = true,
                SystemKey = systemKey,
                GraphJson = graphJson,
                VariablesJson = variablesJson,
                Description = "Auto-upgrades loyalty level based on tenure (years).",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.GraphRules.Add(rule);
        }
        else
        {
            if (!HasSchedule(existing.VariablesJson))
            {
                existing.VariablesJson = MergeSchedule(existing.VariablesJson);
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureDailyStreakRuleAsync(LoyaltyDbContext db, CancellationToken ct)
    {
        const string systemKey = "system.loyalty.daily.streak";
        var existing = await db.GraphRules.FirstOrDefaultAsync(r => r.SystemKey == systemKey, ct);

        var graphJson =
            """
            {
              "start": "audience",
              "nodes": [
                { "id": "audience", "type": "audience_selector", "query": {} },
                { "id": "streak", "type": "streak_daily", "basePoints": "var:basePoints", "stepPoints": "var:stepPoints", "maxPoints": "var:maxPoints" },
                { "id": "award", "type": "award", "pointsVar": "streakBonus" },
                { "id": "end", "type": "end" }
              ],
              "edges": [
                { "from": "audience", "to": "streak" },
                { "from": "streak", "to": "award", "when": true },
                { "from": "streak", "to": "end", "when": false },
                { "from": "award", "to": "end" }
              ]
            }
            """;

        var variablesJson =
            """
            {
              "basePoints": 10,
              "stepPoints": 2,
              "maxPoints": 50,
              "schedule": {
                "kind": "daily",
                "time": "00:05",
                "utcOffsetMinutes": 300
              }
            }
            """;

        if (existing == null)
        {
            var rule = new LoyaltyGraphRule
            {
                Id = Guid.NewGuid(),
                Name = "System: Daily Streak Bonus",
                Topic = "system.cron.v1",
                Priority = 90,
                IsActive = true,
                IsSystem = true,
                SystemKey = systemKey,
                GraphJson = graphJson,
                VariablesJson = variablesJson,
                Description = "Grants daily streak bonus points (once per day).",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.GraphRules.Add(rule);
        }
        else
        {
            if (!HasSchedule(existing.VariablesJson))
            {
                existing.VariablesJson = MergeSchedule(existing.VariablesJson);
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static bool HasSchedule(string? variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            return doc.RootElement.TryGetProperty("schedule", out _);
        }
        catch
        {
            return false;
        }
    }

    private static string MergeSchedule(string? variablesJson)
    {
        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(variablesJson))
        {
            try
            {
                map = JsonSerializer.Deserialize<Dictionary<string, object>>(variablesJson) ?? map;
            }
            catch
            {
                map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

        map["schedule"] = new Dictionary<string, object>
        {
            ["kind"] = "daily",
            ["time"] = "00:05",
            ["utcOffsetMinutes"] = 300
        };

        return JsonSerializer.Serialize(map);
    }
}
