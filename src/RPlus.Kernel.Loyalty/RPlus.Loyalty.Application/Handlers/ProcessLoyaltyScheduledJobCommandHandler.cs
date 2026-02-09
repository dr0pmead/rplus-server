using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Contracts.Domain.Loyalty;
using RPlus.SDK.Contracts.Domain.Notifications;
using RPlus.SDK.Contracts.Domain.Social;
using RPlus.SDK.Eventing;
using RPlus.SDK.Infrastructure.Outbox;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Loyalty.Results;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Handlers;

public sealed record ProcessLoyaltyScheduledJobCommand(Guid JobId) : IRequest<LoyaltyEventProcessResult>;

public sealed class ProcessLoyaltyScheduledJobCommandHandler : IRequestHandler<ProcessLoyaltyScheduledJobCommand, LoyaltyEventProcessResult>
{
    private readonly LoyaltyDbContext _db;
    private readonly IRuntimeGraphClient _runtime;
    private readonly IUserContextProvider _userContext;
    private readonly ILogger<ProcessLoyaltyScheduledJobCommandHandler> _logger;

    public ProcessLoyaltyScheduledJobCommandHandler(
        LoyaltyDbContext db,
        IRuntimeGraphClient runtime,
        IUserContextProvider userContext,
        ILogger<ProcessLoyaltyScheduledJobCommandHandler> logger)
    {
        _db = db;
        _runtime = runtime;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<LoyaltyEventProcessResult> Handle(ProcessLoyaltyScheduledJobCommand request, CancellationToken ct)
    {
        var job = await _db.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == request.JobId, ct);
        if (job == null)
        {
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "JOB_NOT_FOUND", ErrorMessage = "Scheduled job not found." };
        }

        if (string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return new LoyaltyEventProcessResult { Success = job.PointsAwarded > 0, PointsDelta = job.PointsAwarded };
        }

        var rule = await _db.GraphRules.FirstOrDefaultAsync(r => r.Id == job.RuleId, ct);
        if (rule == null || !rule.IsActive)
        {
            job.Status = "Failed";
            job.LastError = "Graph rule not found or inactive.";
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "RULE_NOT_FOUND", ErrorMessage = "Graph rule not found or inactive." };
        }

        if (rule.MaxExecutions.HasValue && rule.ExecutionsCount >= rule.MaxExecutions.Value)
        {
            rule.IsActive = false;
            rule.UpdatedAt = DateTime.UtcNow;
            job.Status = "Completed";
            job.PointsAwarded = 0;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.UpdatedAtUtc = DateTime.UtcNow;
            job.LastError = "Rule execution limit reached.";
            await _db.SaveChangesAsync(ct);
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "RULE_EXHAUSTED", ErrorMessage = "Rule execution limit reached." };
        }

        if (job.UserId == Guid.Empty)
        {
            job.Status = "Failed";
            job.LastError = "UserId is empty.";
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "INVALID_JOB", ErrorMessage = "UserId is empty." };
        }

        var occurredAt = job.RunAtUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(job.RunAtUtc, DateTimeKind.Utc) : job.RunAtUtc.ToUniversalTime();

        var userCtx = await _userContext.GetAsync(job.UserId, occurredAt, ct);

        using var payloadDoc = ParseJsonBestEffort(job.PayloadJson);
        var payload = payloadDoc.RootElement;

        var operationId = string.IsNullOrWhiteSpace(job.OperationId) ? ComputeDeterministicId(rule.Id, job.UserId, occurredAt) : job.OperationId.Trim();
        var eventType = string.IsNullOrWhiteSpace(job.EventType) ? "system.scheduler.fired.v1" : job.EventType.Trim();

        using var ctxDoc = BuildJsonLogicContext(rule.Topic, key: "scheduler", operationId, job.UserId, occurredAt, eventType, payload, userCtx);
        var ctxRoot = ctxDoc.RootElement;
        var eventJson = ctxRoot.GetRawText();

        var (profile, program) = await EnsureProfilesAsync(job.UserId, ct);

        var rules = new[] { rule };

        var appliedRuleIds = new List<string>();
        decimal totalPoints = 0;
        var pendingActions = new List<(Guid RuleId, RuntimeGraphAction Action)>();

        foreach (var gr in rules)
        {
            var already = await _db.GraphRuleExecutions.AsNoTracking().AnyAsync(
                x => x.OperationId == operationId && x.RuleId == gr.Id && x.UserId == job.UserId,
                ct);
            if (already)
            {
                continue;
            }

            var result = await _runtime.ExecuteAsync(new RuntimeGraphRequest(
                RuleId: gr.Id,
                UserId: job.UserId,
                OperationId: operationId,
                GraphJson: gr.GraphJson,
                VariablesJson: gr.VariablesJson,
                EventJson: eventJson,
                OccurredAtUtc: occurredAt,
                StartNodeOverride: null,
                Persist: true), ct);

            if (!result.Success)
            {
                _logger.LogWarning("Runtime execution failed for rule {RuleId} and user {UserId}: {Error}", gr.Id, job.UserId, result.Error ?? "unknown_error");
                continue;
            }

            if (!result.Matched)
            {
                continue;
            }

            _db.GraphRuleExecutions.Add(new LoyaltyGraphRuleExecution
            {
                Id = Guid.NewGuid(),
                RuleId = gr.Id,
                UserId = job.UserId,
                OperationId = operationId,
                PointsApplied = result.PointsDelta,
                CreatedAt = DateTime.UtcNow
            });

            appliedRuleIds.Add(gr.Id.ToString());
            totalPoints += result.PointsDelta;
            if (result.Actions.Count > 0)
            {
                pendingActions.AddRange(result.Actions.Select(a => (gr.Id, a)));
            }

            if (gr.MaxExecutions.HasValue)
            {
                gr.ExecutionsCount += 1;
                if (gr.ExecutionsCount >= gr.MaxExecutions.Value)
                {
                    gr.IsActive = false;
                }
                gr.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (totalPoints <= 0 && pendingActions.Count == 0)
        {
            job.Status = "Completed";
            job.PointsAwarded = 0;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "GRAPH_NOT_MATCHED", ErrorMessage = "No graph rules matched the scheduled trigger." };
        }

        if (totalPoints > 0)
        {
            profile.ApplyPoints(totalPoints);
            program.PointsBalance = profile.PointsBalance;
            program.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (pendingActions.Count > 0)
        {
            ApplyActions(
                job.UserId,
                operationId,
                occurredAt,
                rule.Topic,
                eventType,
                ctxRoot,
                userCtx,
                program,
                pendingActions);
        }

        if (totalPoints > 0)
        {
            var requested = new LoyaltyPointsAccrualRequested_v1(job.UserId.ToString(), totalPoints, operationId)
            {
                MessageId = Guid.NewGuid(),
                SourceService = "rplus-loyalty",
                Timestamp = occurredAt
            };

            var accrued = new LoyaltyPointsAccrued_v1(job.UserId.ToString(), totalPoints, profile.PointsBalance, operationId)
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
                aggregateId: job.UserId.ToString(),
                traceId: traceId)
            {
                OccurredAt = occurredAt,
                Metadata = new Dictionary<string, string> { ["topic"] = rule.Topic, ["eventType"] = eventType, ["scheduledJobId"] = job.Id.ToString() }
            };

            var accruedEnvelope = new EventEnvelope<LoyaltyPointsAccrued_v1>(
                accrued,
                source: "rplus-loyalty",
                eventType: LoyaltyEventTopics.PointsAccrued,
                aggregateId: job.UserId.ToString(),
                traceId: traceId)
            {
                OccurredAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string> { ["topic"] = rule.Topic, ["eventType"] = eventType, ["scheduledJobId"] = job.Id.ToString() }
            };

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Id = requestedEnvelope.EventId,
                EventName = LoyaltyEventTopics.PointsAccrualRequested,
                Payload = JsonSerializer.Serialize(requestedEnvelope),
                CreatedAt = DateTime.UtcNow,
                AggregateId = job.UserId.ToString()
            });

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Id = accruedEnvelope.EventId,
                EventName = LoyaltyEventTopics.PointsAccrued,
                Payload = JsonSerializer.Serialize(accruedEnvelope),
                CreatedAt = DateTime.UtcNow,
                AggregateId = job.UserId.ToString()
            });
        }

        job.Status = "Completed";
        job.PointsAwarded = totalPoints;
        job.CompletedAtUtc = DateTime.UtcNow;
        job.UpdatedAtUtc = DateTime.UtcNow;
        job.LastError = null;

        await _db.SaveChangesAsync(ct);

        return new LoyaltyEventProcessResult
        {
            Success = true,
            PointsDelta = totalPoints,
            NewBalance = profile.PointsBalance,
            AppliedRuleIds = appliedRuleIds
        };
    }

    private async Task<(LoyaltyProfile Profile, LoyaltyProgramProfile Program)> EnsureProfilesAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? LoyaltyProfile.Create(userId);
        if (_db.Entry(profile).State == EntityState.Detached)
        {
            _db.Profiles.Add(profile);
        }

        var program = await _db.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
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
            _db.ProgramProfiles.Add(program);
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

    private void ApplyActions(
        Guid userId,
        string operationId,
        DateTime occurredAtUtc,
        string topic,
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
                        ["topic"] = topic,
                        ["eventType"] = eventType,
                        ["ruleId"] = ruleId.ToString(),
                        ["nodeId"] = action.NodeId,
                        ["operationId"] = operationId
                    }
                };

                _db.OutboxMessages.Add(new OutboxMessage
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
                        ["topic"] = topic,
                        ["eventType"] = eventType,
                        ["ruleId"] = ruleId.ToString(),
                        ["nodeId"] = action.NodeId,
                        ["operationId"] = operationId
                    }
                };

                _db.OutboxMessages.Add(new OutboxMessage
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

    private static string ComputeDeterministicId(Guid ruleId, Guid userId, DateTime runAtUtc)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{ruleId:N}:{userId:N}:{runAtUtc:O}"));
        return Convert.ToHexString(bytes);
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

    private static JsonDocument ParseJsonBestEffort(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonDocument.Parse("{}");
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static JsonDocument BuildJsonLogicContext(
        string topic,
        string key,
        string operationId,
        Guid userId,
        DateTime occurredAtUtc,
        string eventType,
        JsonElement payload,
        UserContext? userContext)
    {
        var bytes = new ArrayBufferWriter<byte>(16 * 1024);
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            writer.WriteString("topic", topic);
            writer.WriteString("key", key ?? string.Empty);
            writer.WriteString("operationId", operationId);
            writer.WriteString("userId", userId.ToString());
            writer.WriteString("occurredAt", occurredAtUtc.ToString("O"));
            writer.WriteString("eventType", eventType);
            writer.WritePropertyName("user");
            JsonSerializer.Serialize(writer, userContext);
            writer.WritePropertyName("payload");
            payload.WriteTo(writer);
            writer.WriteEndObject();
        }

        return JsonDocument.Parse(bytes.WrittenSpan.ToArray());
    }
}
