using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Loyalty.Abstractions;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Loyalty.Results;
using LoyaltyProfileModel = RPlus.SDK.Loyalty.Models.LoyaltyProfile;

namespace RPlus.Loyalty.Infrastructure.Services;

public class BasicLoyaltyRuleEvaluator : ILoyaltyRuleEvaluator
{
    private readonly LoyaltyDbContext _dbContext;
    private readonly ILogger<BasicLoyaltyRuleEvaluator> _logger;

    public BasicLoyaltyRuleEvaluator(LoyaltyDbContext dbContext, ILogger<BasicLoyaltyRuleEvaluator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<LoyaltyDecision> EvaluateAsync<TCommand>(TCommand command, LoyaltyProfileModel profile, CancellationToken cancellationToken = default) where TCommand : notnull
    {
        if (command is not LoyaltyTriggerEvent trigger)
        {
            _logger.LogWarning("Unsupported command type {CommandType} passed to Loyalty rule evaluator", typeof(TCommand).Name);
            return new LoyaltyDecision();
        }

        var rules = await _dbContext.LoyaltyRules
            .Where(r => r.IsActive && r.EventType == trigger.EventType)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return new LoyaltyDecision();
        }

        var applied = new List<LoyaltyRule>();
        decimal total = 0;

        foreach (var rule in rules)
        {
            if (rule.Matches(trigger.Metadata))
            {
                var ruleType = (rule.RuleType ?? "simple_points").Trim().ToLowerInvariant();

                switch (ruleType)
                {
                    case "simple_points":
                        total += rule.Points;
                        applied.Add(rule);
                        break;

                    case "streak_days":
                        if (TryApplyStreakDays(rule, trigger, profile, out var delta))
                        {
                            total += delta;
                            applied.Add(rule);
                        }
                        break;

                    case "count_within_window":
                        if (TryApplyCountWithinWindow(rule, trigger, profile, out var delta2))
                        {
                            total += delta2;
                            applied.Add(rule);
                        }
                        break;

                    default:
                        _logger.LogWarning("Unknown Loyalty RuleType={RuleType} for RuleId={RuleId}", ruleType, rule.Id);
                        break;
                }
            }
        }

        return new LoyaltyDecision
        {
            PointsDelta = total,
            AppliedRules = applied.Cast<RPlus.SDK.Loyalty.Models.LoyaltyRule>().ToList()
        };
    }

    private bool TryApplyStreakDays(LoyaltyRule rule, LoyaltyTriggerEvent trigger, LoyaltyProfileModel profile, out decimal delta)
    {
        delta = 0;
        var config = ParseConfig(rule.RuleConfigJson);
        var targetDays = GetInt(config, "targetDays", defaultValue: 30);
        var cooldownDays = GetInt(config, "cooldownDays", defaultValue: 30);

        var state = GetOrCreateState(rule.Id, profile.UserId);
        var nowDate = trigger.OccurredAt.Date;

        var lastDate = GetStateDate(state, "lastDate");
        var streak = GetStateInt(state, "streak", defaultValue: 0);
        var lastAwardedAt = GetStateDateTime(state, "lastAwardedAt");

        if (lastAwardedAt.HasValue && cooldownDays > 0 && (trigger.OccurredAt - lastAwardedAt.Value) < TimeSpan.FromDays(cooldownDays))
        {
            return false;
        }

        if (lastDate.HasValue)
        {
            if (nowDate == lastDate.Value)
            {
                // same day: do not increment
            }
            else if (nowDate == lastDate.Value.AddDays(1))
            {
                streak++;
            }
            else
            {
                streak = 1;
            }
        }
        else
        {
            streak = 1;
        }

        Set(state, "lastDate", nowDate.ToString("O"));
        Set(state, "streak", streak);

        if (streak >= targetDays)
        {
            Set(state, "lastAwardedAt", trigger.OccurredAt.ToString("O"));
            Set(state, "streak", 0);
            delta = rule.Points;
            return true;
        }

        return false;
    }

    private bool TryApplyCountWithinWindow(LoyaltyRule rule, LoyaltyTriggerEvent trigger, LoyaltyProfileModel profile, out decimal delta)
    {
        delta = 0;
        var config = ParseConfig(rule.RuleConfigJson);
        var threshold = GetInt(config, "threshold", defaultValue: 5);
        var windowDays = GetInt(config, "windowDays", defaultValue: 3650);
        var distinctByDay = GetBool(config, "distinctByDay", defaultValue: false);
        var cooldownDays = GetInt(config, "cooldownDays", defaultValue: 0);

        var state = GetOrCreateState(rule.Id, profile.UserId);
        var now = trigger.OccurredAt;

        var windowStart = GetStateDateTime(state, "windowStart") ?? now;
        var count = GetStateInt(state, "count", defaultValue: 0);
        var lastCountedDay = GetStateDate(state, "lastCountedDay");
        var lastAwardedAt = GetStateDateTime(state, "lastAwardedAt");

        if (lastAwardedAt.HasValue && cooldownDays > 0 && (now - lastAwardedAt.Value) < TimeSpan.FromDays(cooldownDays))
        {
            return false;
        }

        if (windowDays > 0 && (now - windowStart) > TimeSpan.FromDays(windowDays))
        {
            windowStart = now;
            count = 0;
            lastCountedDay = null;
        }

        if (distinctByDay)
        {
            var day = now.Date;
            if (lastCountedDay == day)
            {
                // already counted today
            }
            else
            {
                count++;
                lastCountedDay = day;
            }
        }
        else
        {
            count++;
        }

        Set(state, "windowStart", windowStart.ToString("O"));
        Set(state, "count", count);
        if (lastCountedDay.HasValue)
        {
            Set(state, "lastCountedDay", lastCountedDay.Value.ToString("O"));
        }

        if (count >= threshold)
        {
            Set(state, "lastAwardedAt", now.ToString("O"));
            Set(state, "count", 0);
            delta = rule.Points;
            return true;
        }

        return false;
    }

    private LoyaltyRuleState GetOrCreateState(Guid ruleId, Guid userId)
    {
        var state = _dbContext.RuleStates.FirstOrDefault(s => s.RuleId == ruleId && s.UserId == userId);
        if (state != null)
        {
            state.UpdatedAt = DateTime.UtcNow;
            return state;
        }

        state = new LoyaltyRuleState
        {
            RuleId = ruleId,
            UserId = userId,
            StateJson = "{}",
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.RuleStates.Add(state);
        return state;
    }

    private static Dictionary<string, JsonElement> ParseConfig(string? json)
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
        return defaultValue;
    }

    private static DateTime? GetDateTime(Dictionary<string, JsonElement> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (DateTime.TryParse(value.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt;
        }

        return null;
    }

    private static DateTime? GetDate(Dictionary<string, JsonElement> dict, string key)
    {
        var dt = GetDateTime(dict, key);
        return dt?.Date;
    }

    private static int GetStateInt(LoyaltyRuleState state, string key, int defaultValue) =>
        GetInt(ParseConfig(state.StateJson), key, defaultValue);

    private static DateTime? GetStateDateTime(LoyaltyRuleState state, string key) =>
        GetDateTime(ParseConfig(state.StateJson), key);

    private static DateTime? GetStateDate(LoyaltyRuleState state, string key) =>
        GetDate(ParseConfig(state.StateJson), key);

    private static void Set(LoyaltyRuleState state, string key, object value)
    {
        var dict = ParseConfig(state.StateJson);
        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        dict[key] = doc.RootElement.Clone();
        state.StateJson = JsonSerializer.Serialize(dict);
        state.UpdatedAt = DateTime.UtcNow;
    }
}
