using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Contracts.Domain.Loyalty;
using RPlus.SDK.Contracts.Domain.Notifications;
using RPlus.SDK.Contracts.Domain.Social;
using RPlus.SDK.Loyalty.Abstractions;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Loyalty.Results;
using RPlus.SDK.Infrastructure.Outbox;
using RPlus.SDK.Eventing;
using RPlus.Loyalty.Application.Abstractions;

namespace RPlus.Loyalty.Application.Handlers;

public record ProcessLoyaltyEventCommand(LoyaltyTriggerEvent Trigger) : IRequest<LoyaltyEventProcessResult>;

public class ProcessLoyaltyEventCommandHandler : IRequestHandler<ProcessLoyaltyEventCommand, LoyaltyEventProcessResult>
{
    private readonly LoyaltyDbContext _dbContext;
    private readonly ILoyaltyRuleEvaluator _ruleEvaluator;
    private readonly IRuntimeGraphClient _runtime;
    private readonly IUserContextProvider _userContext;
    private readonly ILogger<ProcessLoyaltyEventCommandHandler> _logger;

    public ProcessLoyaltyEventCommandHandler(
        LoyaltyDbContext dbContext,
        ILoyaltyRuleEvaluator ruleEvaluator,
        IRuntimeGraphClient runtime,
        IUserContextProvider userContext,
        ILogger<ProcessLoyaltyEventCommandHandler> logger)
    {
        _dbContext = dbContext;
        _ruleEvaluator = ruleEvaluator;
        _runtime = runtime;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<LoyaltyEventProcessResult> Handle(ProcessLoyaltyEventCommand request, CancellationToken cancellationToken)
    {
        var trigger = request.Trigger ?? throw new ArgumentNullException(nameof(request.Trigger));

        if (trigger.UserId == Guid.Empty || string.IsNullOrWhiteSpace(trigger.EventType))
        {
            return new LoyaltyEventProcessResult
            {
                ErrorCode = "INVALID_TRIGGER",
                ErrorMessage = "EventType and UserId must be provided."
            };
        }

        var operationId = string.IsNullOrWhiteSpace(trigger.OperationId)
            ? $"{trigger.EventType}:{trigger.UserId}:{trigger.OccurredAt:O}"
            : trigger.OperationId.Trim();

        var graphRules = await _dbContext.GraphRules
            .Where(r => r.IsActive && r.Topic == trigger.EventType)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        if (graphRules.Count > 0)
        {
            return await ProcessGraphRulesAsync(trigger, operationId, graphRules, cancellationToken);
        }

        var existingExecutions = await _dbContext.RuleExecutions
            .AsNoTracking()
            .Where(e => e.OperationId == operationId)
            .ToListAsync(cancellationToken);

        if (existingExecutions.Count > 0)
        {
            var existingProfile = await _dbContext.Profiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == trigger.UserId, cancellationToken);

            return new LoyaltyEventProcessResult
            {
                Success = true,
                PointsDelta = existingExecutions.Sum(e => e.PointsApplied),
                NewBalance = existingProfile?.PointsBalance ?? existingExecutions.Sum(e => e.PointsApplied),
                AppliedRuleIds = existingExecutions.Select(e => e.RuleId.ToString()).Distinct().ToList()
            };
        }

        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.UserId == trigger.UserId, cancellationToken)
            ?? LoyaltyProfile.Create(trigger.UserId);

        if (_dbContext.Entry(profile).State == EntityState.Detached)
        {
            _dbContext.Profiles.Add(profile);
        }

        var decision = await _ruleEvaluator.EvaluateAsync(trigger, profile, cancellationToken);
        if (decision.PointsDelta <= 0)
        {
            _logger.LogInformation("No loyalty rules matched for EventType={EventType} UserId={UserId}", trigger.EventType, trigger.UserId);
            return new LoyaltyEventProcessResult
            {
                Success = false,
                ErrorCode = "RULES_NOT_MATCHED",
                ErrorMessage = "No active loyalty rules matched the incoming event."
            };
        }

        var metadataHash = ComputeMetadataHash(trigger.Metadata);

        profile.ApplyPoints(decision.PointsDelta);
        await UpsertProgramProfileAsync(profile, cancellationToken);

        var appliedRuleIds = new List<string>();
        foreach (var rule in decision.AppliedRules.OfType<LoyaltyRule>())
        {
            var execution = LoyaltyRuleExecution.Create(
                rule.Id,
                profile.Id,
                profile.UserId,
                trigger.EventType,
                operationId,
                rule.Points,
                metadataHash);

            _dbContext.RuleExecutions.Add(execution);
            appliedRuleIds.Add(rule.Id.ToString());
        }

        if (decision.AppliedRules.Count == 0)
        {
            // Fall back to a single aggregated execution if evaluator did not provide rule details.
            var execution = LoyaltyRuleExecution.Create(
                Guid.Empty,
                profile.Id,
                profile.UserId,
                trigger.EventType,
                operationId,
                decision.PointsDelta,
                metadataHash);
            _dbContext.RuleExecutions.Add(execution);
        }

        var accrualRequested = new LoyaltyPointsAccrualRequested_v1(
            trigger.UserId.ToString(),
            decision.PointsDelta,
            operationId)
        {
            MessageId = Guid.NewGuid(),
            SourceService = trigger.Source,
            Timestamp = trigger.OccurredAt
        };

        var accrued = new LoyaltyPointsAccrued_v1(
            trigger.UserId.ToString(),
            decision.PointsDelta,
            profile.PointsBalance,
            operationId)
        {
            MessageId = Guid.NewGuid(),
            SourceService = "rplus-loyalty",
            Timestamp = DateTime.UtcNow
        };

        var traceId = Guid.NewGuid();

        var accrualRequestedEnvelope = new EventEnvelope<LoyaltyPointsAccrualRequested_v1>(
            accrualRequested,
            source: string.IsNullOrWhiteSpace(trigger.Source) ? "rplus-loyalty" : trigger.Source,
            eventType: LoyaltyEventTopics.PointsAccrualRequested,
            aggregateId: trigger.UserId.ToString(),
            traceId: traceId)
        {
            OccurredAt = trigger.OccurredAt,
            Metadata = trigger.Metadata ?? new Dictionary<string, string>()
        };

        var accruedEnvelope = new EventEnvelope<LoyaltyPointsAccrued_v1>(
            accrued,
            source: "rplus-loyalty",
            eventType: LoyaltyEventTopics.PointsAccrued,
            aggregateId: trigger.UserId.ToString(),
            traceId: traceId)
        {
            OccurredAt = DateTime.UtcNow,
            Metadata = trigger.Metadata ?? new Dictionary<string, string>()
        };

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = accrualRequestedEnvelope.EventId,
            EventName = LoyaltyEventTopics.PointsAccrualRequested,
            Payload = JsonSerializer.Serialize(accrualRequestedEnvelope),
            CreatedAt = DateTime.UtcNow,
            AggregateId = trigger.UserId.ToString()
        });

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = accruedEnvelope.EventId,
            EventName = LoyaltyEventTopics.PointsAccrued,
            Payload = JsonSerializer.Serialize(accruedEnvelope),
            CreatedAt = DateTime.UtcNow,
            AggregateId = trigger.UserId.ToString()
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoyaltyEventProcessResult
        {
            Success = true,
            PointsDelta = decision.PointsDelta,
            NewBalance = profile.PointsBalance,
            AppliedRuleIds = appliedRuleIds
        };
    }

    private async Task<LoyaltyEventProcessResult> ProcessGraphRulesAsync(
        LoyaltyTriggerEvent trigger,
        string operationId,
        IReadOnlyList<LoyaltyGraphRule> graphRules,
        CancellationToken ct)
    {
        var occurredAt = trigger.OccurredAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(trigger.OccurredAt, DateTimeKind.Utc)
            : trigger.OccurredAt.ToUniversalTime();

        var userCtx = await _userContext.GetAsync(trigger.UserId, occurredAt, ct);
        using var ctxDoc = BuildJsonLogicContext(trigger.EventType, operationId, trigger.UserId, occurredAt, trigger.Metadata);
        var eventJson = ctxDoc.RootElement.GetRawText();

        var (profile, program) = await EnsureProfilesAsync(trigger.UserId, ct);

        var appliedRuleIds = new List<string>();
        decimal totalPoints = 0;
        var pendingActions = new List<(Guid RuleId, RuntimeGraphAction Action)>();

        foreach (var rule in graphRules)
        {
            if (IsRuleExhausted(rule))
            {
                if (rule.IsActive)
                {
                    rule.IsActive = false;
                    rule.UpdatedAt = DateTime.UtcNow;
                }
                continue;
            }

            var already = await _dbContext.GraphRuleExecutions.AsNoTracking().AnyAsync(
                x => x.OperationId == operationId && x.RuleId == rule.Id && x.UserId == trigger.UserId,
                ct);
            if (already)
            {
                continue;
            }

            var result = await _runtime.ExecuteAsync(new RuntimeGraphRequest(
                RuleId: rule.Id,
                UserId: trigger.UserId,
                OperationId: operationId,
                GraphJson: rule.GraphJson,
                VariablesJson: rule.VariablesJson,
                EventJson: eventJson,
                OccurredAtUtc: occurredAt,
                StartNodeOverride: null,
                Persist: true), ct);

            if (!result.Success)
            {
                _logger.LogWarning("Runtime execution failed for rule {RuleId} and user {UserId}: {Error}", rule.Id, trigger.UserId, result.Error ?? "unknown_error");
                continue;
            }

            if (!result.Matched)
            {
                continue;
            }

            _dbContext.GraphRuleExecutions.Add(new LoyaltyGraphRuleExecution
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                UserId = trigger.UserId,
                OperationId = operationId,
                PointsApplied = result.PointsDelta,
                CreatedAt = DateTime.UtcNow
            });

            appliedRuleIds.Add(rule.Id.ToString());
            totalPoints += result.PointsDelta;
            if (result.Actions.Count > 0)
            {
                pendingActions.AddRange(result.Actions.Select(a => (rule.Id, a)));
            }

            if (rule.MaxExecutions.HasValue)
            {
                rule.ExecutionsCount += 1;
                if (rule.ExecutionsCount >= rule.MaxExecutions.Value)
                {
                    rule.IsActive = false;
                }
                rule.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (totalPoints <= 0 && pendingActions.Count == 0)
        {
            return new LoyaltyEventProcessResult
            {
                Success = false,
                ErrorCode = "GRAPH_NOT_MATCHED",
                ErrorMessage = "No graph rules matched the incoming event."
            };
        }

        if (totalPoints > 0)
        {
            profile.ApplyPoints(totalPoints);
            program.PointsBalance = profile.PointsBalance;
            program.UpdatedAtUtc = DateTime.UtcNow;

            EmitPointsEvents(trigger.UserId, totalPoints, profile.PointsBalance, operationId, occurredAt, trigger.EventType);
        }

        if (pendingActions.Count > 0)
        {
            ApplyActions(
                trigger.UserId,
                operationId,
                occurredAt,
                trigger.EventType,
                ctxDoc.RootElement,
                userCtx,
                program,
                pendingActions);
        }

        await _dbContext.SaveChangesAsync(ct);

        return new LoyaltyEventProcessResult
        {
            Success = true,
            PointsDelta = totalPoints,
            NewBalance = profile.PointsBalance,
            AppliedRuleIds = appliedRuleIds
        };
    }

    private static string? ComputeMetadataHash(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return null;
        }

        var ordered = metadata.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        foreach (var kvp in ordered)
        {
            builder.Append(kvp.Key).Append('=').Append(kvp.Value).Append(';');
        }

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private async Task UpsertProgramProfileAsync(LoyaltyProfile profile, CancellationToken ct)
    {
        var program = await _dbContext.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == profile.UserId, ct);
        if (program == null)
        {
            program = new LoyaltyProgramProfile
            {
                UserId = profile.UserId,
                Level = "Base",
                TagsJson = "[]",
                PointsBalance = profile.PointsBalance,
                CreatedAtUtc = profile.CreatedAt,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.ProgramProfiles.Add(program);
            return;
        }

        if (string.IsNullOrWhiteSpace(program.Level))
        {
            program.Level = "Base";
        }

        program.PointsBalance = profile.PointsBalance;
        program.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<(LoyaltyProfile Profile, LoyaltyProgramProfile Program)> EnsureProfilesAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _dbContext.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? LoyaltyProfile.Create(userId);
        if (_dbContext.Entry(profile).State == EntityState.Detached)
        {
            _dbContext.Profiles.Add(profile);
        }

        var program = await _dbContext.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (program == null)
        {
            program = new LoyaltyProgramProfile
            {
                UserId = userId,
                Level = "Base",
                TagsJson = "[]",
                PointsBalance = profile.PointsBalance,
                CreatedAtUtc = profile.CreatedAt,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.ProgramProfiles.Add(program);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(program.Level))
            {
                program.Level = "Base";
            }

            program.PointsBalance = profile.PointsBalance;
            program.UpdatedAtUtc = DateTime.UtcNow;
        }

        return (profile, program);
    }

    private void EmitPointsEvents(Guid userId, decimal pointsDelta, decimal newBalance, string operationId, DateTime occurredAt, string eventType)
    {
        var requested = new LoyaltyPointsAccrualRequested_v1(userId.ToString(), pointsDelta, operationId)
        {
            MessageId = Guid.NewGuid(),
            SourceService = "rplus-loyalty",
            Timestamp = occurredAt
        };

        var accrued = new LoyaltyPointsAccrued_v1(userId.ToString(), pointsDelta, newBalance, operationId)
        {
            MessageId = Guid.NewGuid(),
            SourceService = "rplus-loyalty",
            Timestamp = DateTime.UtcNow
        };

        var traceId = Guid.NewGuid();
        var requestedEnvelope = new EventEnvelope<LoyaltyPointsAccrualRequested_v1>(
            requested,
            source: "rplus-loyalty",
            eventType: LoyaltyEventTopics.PointsAccrualRequested,
            aggregateId: userId.ToString(),
            traceId: traceId)
        {
            OccurredAt = occurredAt,
            Metadata = new Dictionary<string, string> { ["eventType"] = eventType }
        };

        var accruedEnvelope = new EventEnvelope<LoyaltyPointsAccrued_v1>(
            accrued,
            source: "rplus-loyalty",
            eventType: LoyaltyEventTopics.PointsAccrued,
            aggregateId: userId.ToString(),
            traceId: traceId)
        {
            OccurredAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string> { ["eventType"] = eventType }
        };

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = requestedEnvelope.EventId,
            EventName = LoyaltyEventTopics.PointsAccrualRequested,
            Payload = JsonSerializer.Serialize(requestedEnvelope),
            CreatedAt = DateTime.UtcNow,
            AggregateId = userId.ToString()
        });

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = accruedEnvelope.EventId,
            EventName = LoyaltyEventTopics.PointsAccrued,
            Payload = JsonSerializer.Serialize(accruedEnvelope),
            CreatedAt = DateTime.UtcNow,
            AggregateId = userId.ToString()
        });
    }

    private void ApplyActions(
        Guid userId,
        string operationId,
        DateTime occurredAtUtc,
        string eventType,
        JsonElement ctxRoot,
        UserContext? userCtx,
        LoyaltyProgramProfile program,
        IReadOnlyList<(Guid RuleId, RuntimeGraphAction Action)> actions)
    {
        foreach (var (ruleId, action) in actions)
        {
            if (string.Equals(action.Kind, "update_profile", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = DeserializeBestEffort<UpdateProfileActionConfig>(action.DataJson);
                if (cfg == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cfg.SetLevel))
                {
                    program.Level = cfg.SetLevel.Trim();
                }

                if (cfg.AddTags.Length > 0)
                {
                    var current = ParseTags(program.TagsJson);
                    foreach (var tag in cfg.AddTags)
                    {
                        if (string.IsNullOrWhiteSpace(tag))
                        {
                            continue;
                        }

                        current.Add(tag.Trim());
                    }

                    program.TagsJson = JsonSerializer.Serialize(current.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray());
                }

                program.UpdatedAtUtc = DateTime.UtcNow;
                continue;
            }

            if (string.Equals(action.Kind, "notification", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = DeserializeBestEffort<NotificationActionConfig>(action.DataJson);
                if (cfg == null)
                {
                    continue;
                }

                var title = RenderMustache(cfg.Title, ctxRoot, userCtx, program);
                var body = RenderMustache(cfg.Body, ctxRoot, userCtx, program);

                var requested = new NotificationDispatchRequested_v1(
                    userId.ToString(),
                    cfg.Channel,
                    title,
                    body,
                    operationId,
                    ruleId.ToString(),
                    action.NodeId)
                {
                    MessageId = CreateDeterministicGuid($"notif:{operationId}:{ruleId:N}:{userId:N}:{action.NodeId}"),
                    SourceService = "rplus-loyalty",
                    Timestamp = occurredAtUtc
                };

                var envelope = new EventEnvelope<NotificationDispatchRequested_v1>(
                    requested,
                    source: "rplus-loyalty",
                    eventType: NotificationsEventTopics.DispatchRequested,
                    aggregateId: userId.ToString(),
                    traceId: Guid.NewGuid())
                {
                    EventId = requested.MessageId,
                    OccurredAt = occurredAtUtc,
                    Metadata = new Dictionary<string, string>
                    {
                        ["eventType"] = eventType,
                        ["ruleId"] = ruleId.ToString(),
                        ["nodeId"] = action.NodeId,
                        ["operationId"] = operationId
                    }
                };

                _dbContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = envelope.EventId,
                    EventName = NotificationsEventTopics.DispatchRequested,
                    Payload = JsonSerializer.Serialize(envelope),
                    CreatedAt = DateTime.UtcNow,
                    AggregateId = userId.ToString()
                });

                continue;
            }

            if (string.Equals(action.Kind, "feed_post", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = DeserializeBestEffort<FeedPostActionConfig>(action.DataJson);
                if (cfg == null)
                {
                    continue;
                }

                var content = RenderMustache(cfg.Content, ctxRoot, userCtx, program);

                var requested = new SocialFeedPostRequested_v1(
                    userId.ToString(),
                    cfg.Channel,
                    content,
                    operationId,
                    ruleId.ToString(),
                    action.NodeId)
                {
                    MessageId = CreateDeterministicGuid($"feed:{operationId}:{ruleId:N}:{userId:N}:{action.NodeId}"),
                    SourceService = "rplus-loyalty",
                    Timestamp = occurredAtUtc
                };

                var envelope = new EventEnvelope<SocialFeedPostRequested_v1>(
                    requested,
                    source: "rplus-loyalty",
                    eventType: SocialEventTopics.FeedPostRequested,
                    aggregateId: userId.ToString(),
                    traceId: Guid.NewGuid())
                {
                    EventId = requested.MessageId,
                    OccurredAt = occurredAtUtc,
                    Metadata = new Dictionary<string, string>
                    {
                        ["eventType"] = eventType,
                        ["ruleId"] = ruleId.ToString(),
                        ["nodeId"] = action.NodeId,
                        ["operationId"] = operationId
                    }
                };

                _dbContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = envelope.EventId,
                    EventName = SocialEventTopics.FeedPostRequested,
                    Payload = JsonSerializer.Serialize(envelope),
                    CreatedAt = DateTime.UtcNow,
                    AggregateId = userId.ToString()
                });

                continue;
            }
        }
    }

    private static HashSet<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var tags = JsonSerializer.Deserialize<string[]>(tagsJson);
            return tags == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string RenderMustache(string template, JsonElement ctxRoot, UserContext? userCtx, LoyaltyProgramProfile program)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var result = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            var start = template.IndexOf("{{", i, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(template, i, template.Length - i);
                break;
            }

            result.Append(template, i, start - i);
            var end = template.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                result.Append(template, start, template.Length - start);
                break;
            }

            var path = template.Substring(start + 2, end - (start + 2)).Trim();
            var resolved = ResolveTemplateValue(path, ctxRoot, userCtx, program) ?? string.Empty;
            result.Append(resolved);

            i = end + 2;
        }

        return result.ToString();
    }

    private static string? ResolveTemplateValue(string path, JsonElement ctxRoot, UserContext? userCtx, LoyaltyProgramProfile program)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
        {
            var key = path[5..].Trim();
            if (string.Equals(key, "Level", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(program.Level) ? "Base" : program.Level;
            }

            if (string.Equals(key, "Tags", StringComparison.OrdinalIgnoreCase))
            {
                var tags = ParseTags(program.TagsJson);
                return string.Join(",", tags);
            }

            if (userCtx == null)
            {
                return null;
            }

            if (string.Equals(key, "FirstName", StringComparison.OrdinalIgnoreCase)) return userCtx.FirstName;
            if (string.Equals(key, "LastName", StringComparison.OrdinalIgnoreCase)) return userCtx.LastName;
            if (string.Equals(key, "PreferredName", StringComparison.OrdinalIgnoreCase)) return userCtx.PreferredName;
            if (string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase)) return userCtx.Status;
            if (string.Equals(key, "TenureDays", StringComparison.OrdinalIgnoreCase)) return userCtx.TenureDays.ToString();
            if (string.Equals(key, "TenureYears", StringComparison.OrdinalIgnoreCase)) return userCtx.TenureYears.ToString();
            if (string.Equals(key, "IsVip", StringComparison.OrdinalIgnoreCase)) return userCtx.IsVip ? "true" : "false";
            if (string.Equals(key, "IsBirthdayToday", StringComparison.OrdinalIgnoreCase)) return userCtx.IsBirthdayToday ? "true" : "false";
            if (string.Equals(key, "HasDisability", StringComparison.OrdinalIgnoreCase)) return userCtx.HasDisability ? "true" : "false";
            if (string.Equals(key, "ChildrenCount", StringComparison.OrdinalIgnoreCase)) return userCtx.ChildrenCount.ToString();
            if (string.Equals(key, "IsBoss", StringComparison.OrdinalIgnoreCase)) return userCtx.IsBoss ? "true" : "false";
            if (string.Equals(key, "CreatedAtUtc", StringComparison.OrdinalIgnoreCase)) return userCtx.CreatedAtUtc.ToString("O");
        }

        return TryExtractString(ctxRoot, path);
    }

    private static T? DeserializeBestEffort<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return default;
        }
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        return new Guid(bytes);
    }

    private sealed record UpdateProfileActionConfig(string? SetLevel, string[] AddTags);

    private sealed record NotificationActionConfig(string Channel, string Title, string Body);

    private sealed record FeedPostActionConfig(string Channel, string Content);

    private static JsonDocument BuildJsonLogicContext(
        string eventType,
        string operationId,
        Guid userId,
        DateTime occurredAtUtc,
        IReadOnlyDictionary<string, string>? metadata)
    {
        var bytes = new ArrayBufferWriter<byte>(8 * 1024);
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            writer.WriteString("eventType", eventType);
            writer.WriteString("operationId", operationId);
            writer.WriteString("userId", userId.ToString());
            writer.WriteString("occurredAt", occurredAtUtc.ToString("O"));

            writer.WritePropertyName("metadata");
            JsonSerializer.Serialize(writer, metadata ?? new Dictionary<string, string>());

            writer.WritePropertyName("payload");
            JsonSerializer.Serialize(writer, metadata ?? new Dictionary<string, string>());

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(bytes.WrittenSpan.ToArray());
    }

    private static string? TryExtractString(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!TryExtractElement(root, path, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static bool TryExtractElement(JsonElement root, string path, out JsonElement value)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (!TryGetPropertyCaseInsensitive(current, segment, out var next))
            {
                value = default;
                return false;
            }

            current = next;
        }

        value = current;
        return true;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in element.EnumerateObject())
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

    private static bool IsRuleExhausted(LoyaltyGraphRule rule)
    {
        return rule.MaxExecutions.HasValue && rule.ExecutionsCount >= rule.MaxExecutions.Value;
    }
}
