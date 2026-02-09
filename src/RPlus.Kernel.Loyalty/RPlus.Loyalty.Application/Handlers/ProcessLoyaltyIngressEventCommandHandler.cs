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
using RPlus.SDK.Eventing.SchemaRegistry;
using RPlus.SDK.Infrastructure.Outbox;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Loyalty.Results;
using System.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Handlers;

public sealed record ProcessLoyaltyIngressEventCommand(
    string Topic,
    string Key,
    string ValueJson,
    IReadOnlyList<EventSchemaDescriptor> SchemasForTopic) : IRequest<LoyaltyEventProcessResult>;

public sealed class ProcessLoyaltyIngressEventCommandHandler : IRequestHandler<ProcessLoyaltyIngressEventCommand, LoyaltyEventProcessResult>
{
    private readonly LoyaltyDbContext _db;
    private readonly IRuntimeGraphClient _runtime;
    private readonly IUserContextProvider _userContext;
    private readonly ILoyaltyLevelCatalog _levelCatalog;
    private readonly ILogger<ProcessLoyaltyIngressEventCommandHandler> _logger;

    public ProcessLoyaltyIngressEventCommandHandler(
        LoyaltyDbContext db,
        IRuntimeGraphClient runtime,
        IUserContextProvider userContext,
        ILoyaltyLevelCatalog levelCatalog,
        ILogger<ProcessLoyaltyIngressEventCommandHandler> logger)
    {
        _db = db;
        _runtime = runtime;
        _userContext = userContext;
        _levelCatalog = levelCatalog;
        _logger = logger;
    }

    public async Task<LoyaltyEventProcessResult> Handle(ProcessLoyaltyIngressEventCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Topic) || string.IsNullOrWhiteSpace(request.ValueJson))
        {
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "INVALID_INGRESS", ErrorMessage = "Topic and ValueJson must be provided." };
        }

        EventSchemaDescriptor? schema;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(request.ValueJson);
            schema = SelectSchema(request.SchemasForTopic, TryExtractString(doc.RootElement, "EventType"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid JSON received on topic {Topic}.", request.Topic);
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "INVALID_JSON", ErrorMessage = "Incoming event payload is not valid JSON." };
        }

        using (doc)
        {
            if (schema == null)
            {
                return new LoyaltyEventProcessResult { Success = false, ErrorCode = "SCHEMA_NOT_FOUND", ErrorMessage = "No schema matched the incoming event." };
            }

            var root = doc.RootElement;

            var eventType = TryExtractString(root, "EventType") ?? schema.EventType;
            var operationId = TryExtractString(root, schema.Hints.OperationIdPath)
                              ?? TryExtractString(root, "EventId")
                              ?? ComputeDeterministicId(request.Topic, request.Key, request.ValueJson);

            var userIdRaw = TryExtractString(root, schema.Hints.SubjectIdPath)
                            ?? TryExtractString(root, "Payload.UserId")
                            ?? TryExtractString(root, "UserId");

            var userId = Guid.Empty;
            var hasUser = Guid.TryParse(userIdRaw, out userId) && userId != Guid.Empty;
            var userOptional = string.IsNullOrWhiteSpace(schema.Hints.SubjectIdPath);
            if (!hasUser && !userOptional)
            {
                return new LoyaltyEventProcessResult { Success = false, ErrorCode = "SUBJECT_NOT_FOUND", ErrorMessage = "Could not resolve UserId for this event." };
            }

            var occurredAt = TryExtractOccurredAt(root, schema.Hints.OccurredAtPath)
                             ?? TryExtractOccurredAt(root, "OccurredAt")
                             ?? DateTime.UtcNow;

            LoyaltyProfile? profileEntity = null;
            LoyaltyProgramProfile? programProfileEntity = null;
            UserContext? userCtx = null;
            if (hasUser)
            {
                // Auto-provisioning: ensure the user exists in Loyalty bounded context even if no rules match and no points are awarded.
                // This is required for welcome flows that rely on welcome flows before any points activity occurs.
                (profileEntity, programProfileEntity) = await EnsureProfilesAsync(userId, ct);
                userCtx = await _userContext.GetAsync(userId, occurredAt, ct);
            }

            var existingIngress = await _db.IngressEvents
                .FirstOrDefaultAsync(e => e.Topic == request.Topic && e.OperationId == operationId, ct);

            if (existingIngress?.ProcessedAt != null)
            {
                var executions = await _db.GraphRuleExecutions.AsNoTracking()
                    .Where(x => x.OperationId == operationId && (!hasUser || x.UserId == userId))
                    .ToListAsync(ct);

                var prevPoints = executions.Sum(x => x.PointsApplied);
                var hadAnyEffects = executions.Count > 0;

                var profile = hasUser
                    ? await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct)
                    : null;
                return new LoyaltyEventProcessResult
                {
                    Success = hadAnyEffects,
                    PointsDelta = prevPoints,
                    NewBalance = profile?.PointsBalance ?? 0,
                    AppliedRuleIds = await _db.GraphRuleExecutions.AsNoTracking()
                        .Where(x => x.OperationId == operationId && (!hasUser || x.UserId == userId))
                        .Select(x => x.RuleId.ToString())
                        .Distinct()
                        .ToListAsync(ct)
                };
            }

            var ingress = existingIngress ?? new LoyaltyIngressEvent
            {
                Id = Guid.NewGuid(),
                Topic = request.Topic,
                Key = request.Key ?? string.Empty,
                OperationId = operationId,
                EventType = eventType,
                UserId = userId,
                OccurredAt = occurredAt,
                ReceivedAt = DateTime.UtcNow,
                PayloadJson = request.ValueJson
            };

            if (existingIngress == null)
            {
                _db.IngressEvents.Add(ingress);
            }

            if (string.Equals(request.Topic, "system.cron.v1", StringComparison.OrdinalIgnoreCase))
            {
                await TrySyncTenureRuleAsync(ct);
            }

            var rules = await _db.GraphRules
                .Where(r => r.IsActive && r.Topic == request.Topic)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync(ct);

            if (rules.Count == 0)
            {
                ingress.ProcessedAt = DateTime.UtcNow;
                ingress.PointsAwarded = 0;
                await _db.SaveChangesAsync(ct);
                return new LoyaltyEventProcessResult { Success = false, ErrorCode = "NO_GRAPH_RULES", ErrorMessage = "No active graph rules configured for this topic." };
            }

            var payload = ResolvePayloadElement(root, schema);
            using var ctxDoc = BuildJsonLogicContext(request.Topic, request.Key ?? string.Empty, operationId, hasUser ? userId : Guid.Empty, occurredAt, eventType, payload, userCtx);
            var ctxRoot = ctxDoc.RootElement;
            var eventJson = ctxRoot.GetRawText();

            var appliedRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            decimal totalPoints = 0;
            var pendingActions = new List<(Guid RuleId, RuntimeGraphAction Action)>();

            foreach (var rule in rules)
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

                if (!ShouldRunCronRule(rule, occurredAt))
                {
                    continue;
                }

                if (hasUser)
                {
                    var result = await EvaluateRuleForUserAsync(rule, userId, operationId, occurredAt, eventJson, ct);
                    if (result == null)
                    {
                        continue;
                    }

                    _db.GraphRuleExecutions.Add(new LoyaltyGraphRuleExecution
                    {
                        Id = Guid.NewGuid(),
                        RuleId = rule.Id,
                        UserId = userId,
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
                    continue;
                }

                // user-less triggers (e.g. system.cron) must provide an audience selector to produce a user list.
                var selection = await _runtime.ExecuteAsync(new RuntimeGraphRequest(
                    RuleId: rule.Id,
                    UserId: Guid.Empty,
                    OperationId: operationId,
                    GraphJson: rule.GraphJson,
                    VariablesJson: rule.VariablesJson,
                    EventJson: eventJson,
                    OccurredAtUtc: occurredAt,
                    StartNodeOverride: null,
                    Persist: false), ct);

                if (!selection.Success)
                {
                    _logger.LogWarning("Runtime execution failed for rule {RuleId}: {Error}", rule.Id, selection.Error ?? "unknown_error");
                    continue;
                }

                if (selection.AudienceSelection == null || string.IsNullOrWhiteSpace(selection.AudienceSelection.ResumeFromNodeId))
                {
                    continue;
                }

                var audienceUserIds = await ResolveAudienceAsync(selection.AudienceSelection.QueryJson, ct);
                var ruleExhausted = false;
                foreach (var targetUserId in audienceUserIds)
                {
                    ct.ThrowIfCancellationRequested();

                    var (targetProfile, targetProgram) = await EnsureProfilesAsync(targetUserId, ct);
                    var targetUserCtx = await _userContext.GetAsync(targetUserId, occurredAt, ct);
                    using var perUserCtxDoc = BuildJsonLogicContext(request.Topic, request.Key ?? string.Empty, operationId, targetUserId, occurredAt, eventType, payload, targetUserCtx);
                    var perUserCtxRoot = perUserCtxDoc.RootElement;
                    var perUserEventJson = perUserCtxRoot.GetRawText();

                    var perUserResult = await EvaluateRuleForUserAsync(
                        rule,
                        targetUserId,
                        operationId,
                        occurredAt,
                        perUserEventJson,
                        ct,
                        selection.AudienceSelection.ResumeFromNodeId);
                    if (perUserResult == null)
                    {
                        continue;
                    }

                    _db.GraphRuleExecutions.Add(new LoyaltyGraphRuleExecution
                    {
                        Id = Guid.NewGuid(),
                        RuleId = rule.Id,
                        UserId = targetUserId,
                        OperationId = operationId,
                        PointsApplied = perUserResult.PointsDelta,
                        CreatedAt = DateTime.UtcNow
                    });

                    appliedRuleIds.Add(rule.Id.ToString());

                    if (perUserResult.PointsDelta > 0)
                    {
                        targetProfile.ApplyPoints(perUserResult.PointsDelta);
                        targetProgram.PointsBalance = targetProfile.PointsBalance;
                        targetProgram.UpdatedAtUtc = DateTime.UtcNow;
                        totalPoints += perUserResult.PointsDelta;

                        EmitPointsEvents(targetUserId, perUserResult.PointsDelta, targetProfile.PointsBalance, operationId, occurredAt, request.Topic, eventType);
                    }

                    if (perUserResult.Actions.Count > 0)
                    {
                        ApplyActions(
                            targetUserId,
                            operationId,
                            occurredAt,
                            request.Topic,
                            eventType,
                            perUserCtxRoot,
                            targetUserCtx,
                            targetProgram,
                            perUserResult.Actions.Select(a => (rule.Id, a)).ToList());
                    }

                    if (rule.MaxExecutions.HasValue)
                    {
                        rule.ExecutionsCount += 1;
                        if (rule.ExecutionsCount >= rule.MaxExecutions.Value)
                        {
                            rule.IsActive = false;
                            rule.UpdatedAt = DateTime.UtcNow;
                            ruleExhausted = true;
                            break;
                        }
                        rule.UpdatedAt = DateTime.UtcNow;
                    }
                }

                if (ruleExhausted)
                {
                    continue;
                }
            }

        if (totalPoints <= 0 && appliedRuleIds.Count == 0)
        {
            ingress.ProcessedAt = DateTime.UtcNow;
            ingress.PointsAwarded = 0;
            await _db.SaveChangesAsync(ct);
            return new LoyaltyEventProcessResult { Success = false, ErrorCode = "GRAPH_NOT_MATCHED", ErrorMessage = "No graph rules matched the incoming event." };
        }

        if (hasUser)
        {
            if (totalPoints > 0)
            {
                profileEntity!.ApplyPoints(totalPoints);
                programProfileEntity!.PointsBalance = profileEntity.PointsBalance;
                programProfileEntity.UpdatedAtUtc = DateTime.UtcNow;

                EmitPointsEvents(userId, totalPoints, profileEntity.PointsBalance, operationId, occurredAt, request.Topic, eventType);
            }

            if (pendingActions.Count > 0)
            {
                ApplyActions(
                    userId,
                    operationId,
                    occurredAt,
                    request.Topic,
                    eventType,
                    ctxRoot,
                    userCtx,
                    programProfileEntity!,
                    pendingActions);
            }
        }

            ingress.ProcessedAt = DateTime.UtcNow;
            ingress.PointsAwarded = totalPoints;

        await _db.SaveChangesAsync(ct);

        return new LoyaltyEventProcessResult
            {
                Success = true,
                PointsDelta = totalPoints,
                NewBalance = hasUser ? profileEntity!.PointsBalance : 0,
                AppliedRuleIds = appliedRuleIds.ToList()
            };
        }
    }

    private static bool ShouldRunCronRule(LoyaltyGraphRule rule, DateTime occurredAtUtc)
    {
        if (!string.Equals(rule.Topic, "system.cron.v1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(rule.VariablesJson))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(rule.VariablesJson);
            if (!doc.RootElement.TryGetProperty("schedule", out var schedule))
            {
                return true;
            }

            var kind = TryExtractString(schedule, "kind") ?? TryExtractString(schedule, "type");
            if (!string.Equals(kind, "daily", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var timeText = TryExtractString(schedule, "time");
            if (!TimeSpan.TryParse(timeText, out var targetTime))
            {
                return true;
            }

            var offsetMinutes = 0;
            if (schedule.TryGetProperty("utcOffsetMinutes", out var offsetProp) && offsetProp.ValueKind == JsonValueKind.Number)
            {
                offsetMinutes = offsetProp.GetInt32();
            }
            else
            {
                var tzText = TryExtractString(schedule, "timeZone");
                if (!string.IsNullOrWhiteSpace(tzText) && TimeSpan.TryParse(tzText, out var tzOffset))
                {
                    offsetMinutes = (int)tzOffset.TotalMinutes;
                }
            }

            var local = occurredAtUtc.AddMinutes(offsetMinutes);
            return local.Hour == targetTime.Hours && local.Minute == targetTime.Minutes;
        }
        catch
        {
            return true;
        }
    }

    private async Task<RuntimeGraphResult?> EvaluateRuleForUserAsync(
        LoyaltyGraphRule rule,
        Guid userId,
        string operationId,
        DateTime occurredAt,
        string eventJson,
        CancellationToken ct,
        string? startNodeOverride = null)
    {
        var already = await _db.GraphRuleExecutions.AsNoTracking().AnyAsync(
            x => x.OperationId == operationId && x.RuleId == rule.Id && x.UserId == userId,
            ct);
        if (already)
        {
            return null;
        }

        var result = await _runtime.ExecuteAsync(new RuntimeGraphRequest(
            RuleId: rule.Id,
            UserId: userId,
            OperationId: operationId,
            GraphJson: rule.GraphJson,
            VariablesJson: rule.VariablesJson,
            EventJson: eventJson,
            OccurredAtUtc: occurredAt,
            StartNodeOverride: startNodeOverride,
            Persist: true), ct);

        if (!result.Success)
        {
            _logger.LogWarning("Runtime execution failed for rule {RuleId} and user {UserId}: {Error}", rule.Id, userId, result.Error ?? "unknown_error");
            return null;
        }

        if (!result.Matched)
        {
            return null;
        }

        return result;
    }

    private void EmitPointsEvents(Guid userId, decimal pointsDelta, decimal newBalance, string operationId, DateTime occurredAt, string topic, string eventType)
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
            Metadata = new Dictionary<string, string> { ["topic"] = topic, ["eventType"] = eventType }
        };

        var accruedEnvelope = new EventEnvelope<LoyaltyPointsAccrued_v1>(
            accrued,
            source: "rplus-loyalty",
            eventType: LoyaltyEventTopics.PointsAccrued,
            aggregateId: userId.ToString(),
            traceId: traceId)
        {
            OccurredAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string> { ["topic"] = topic, ["eventType"] = eventType }
        };

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = requestedEnvelope.EventId,
            EventName = LoyaltyEventTopics.PointsAccrualRequested,
            Payload = JsonSerializer.Serialize(requestedEnvelope),
            CreatedAt = DateTime.UtcNow,
            AggregateId = userId.ToString()
        });

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = accruedEnvelope.EventId,
            EventName = LoyaltyEventTopics.PointsAccrued,
            Payload = JsonSerializer.Serialize(accruedEnvelope),
            CreatedAt = DateTime.UtcNow,
            AggregateId = userId.ToString()
        });
    }

    private async Task<Guid[]> ResolveAudienceAsync(string queryJson, CancellationToken ct)
    {
        var q = DeserializeBestEffort<AudienceSelectorQuery>(queryJson);
        if (q == null)
        {
            return Array.Empty<Guid>();
        }

        var limit = q.Limit <= 0 ? 1000 : Math.Min(q.Limit, 10_000);

        // Keep the query provider-friendly (InMemory + Npgsql): do complex string-list matching in memory.
        var candidates = await _db.ProgramProfiles.AsNoTracking()
            .Where(p => q.MinPointsBalance == null || p.PointsBalance >= q.MinPointsBalance.Value)
            .Where(p => q.MaxPointsBalance == null || p.PointsBalance <= q.MaxPointsBalance.Value)
            .Select(p => new { p.UserId, p.Level, p.TagsJson, p.PointsBalance })
            .Take(limit)
            .ToListAsync(ct);

        var levelFilter = q.LevelIn != null && q.LevelIn.Length > 0
            ? new HashSet<string>(q.LevelIn.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase)
            : null;

        var hasAny = q.HasTagsAny != null && q.HasTagsAny.Length > 0;
        var hasAll = q.HasTagsAll != null && q.HasTagsAll.Length > 0;
        if (!hasAny && !hasAll && levelFilter == null)
        {
            return candidates.Select(c => c.UserId).Where(id => id != Guid.Empty).Distinct().ToArray();
        }

        var anySet = hasAny ? new HashSet<string>(q.HasTagsAny!, StringComparer.OrdinalIgnoreCase) : null;
        var allSet = hasAll ? new HashSet<string>(q.HasTagsAll!, StringComparer.OrdinalIgnoreCase) : null;

        var result = new List<Guid>(candidates.Count);
        foreach (var c in candidates)
        {
            if (levelFilter != null && (string.IsNullOrWhiteSpace(c.Level) || !levelFilter.Contains(c.Level)))
            {
                continue;
            }

            var tags = ParseTags(c.TagsJson);
            if (anySet != null && !tags.Overlaps(anySet))
            {
                continue;
            }

            if (allSet != null && !allSet.All(tags.Contains))
            {
                continue;
            }

            result.Add(c.UserId);
        }

        return result.Where(id => id != Guid.Empty).Distinct().ToArray();
    }

    private sealed record AudienceSelectorQuery(
        string[]? LevelIn,
        string[]? HasTagsAny,
        string[]? HasTagsAll,
        decimal? MinPointsBalance,
        decimal? MaxPointsBalance,
        int Limit);

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

        // Minimal Mustache: replace {{path}} with resolved values from the execution context.
        // Supports dot paths (e.g. user.FirstName, payload.IpAddress).
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

    private static EventSchemaDescriptor? SelectSchema(IReadOnlyList<EventSchemaDescriptor> candidates, string? envelopeEventType)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(envelopeEventType))
        {
            return candidates.FirstOrDefault(c => string.Equals(c.EventType, envelopeEventType, StringComparison.OrdinalIgnoreCase));
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static JsonElement ResolvePayloadElement(JsonElement root, EventSchemaDescriptor schema)
    {
        if (!schema.Hints.IsEventEnvelope)
        {
            return root;
        }

        var payloadPath = string.IsNullOrWhiteSpace(schema.Hints.EnvelopePayloadPath) ? "Payload" : schema.Hints.EnvelopePayloadPath;
        if (TryExtractElement(root, payloadPath!, out var payload) && payload.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            return payload;
        }

        return root;
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

    private static string ComputeDeterministicId(string topic, string key, string valueJson)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{topic}:{key}:{valueJson}"));
        return Convert.ToHexString(bytes);
    }

    private static DateTime? TryExtractOccurredAt(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!TryExtractElement(root, path, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unixMs))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
        }

        return null;
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

    private const string TenureRuleKey = "system.loyalty.tenure.level";

    private async Task TrySyncTenureRuleAsync(CancellationToken ct)
    {
        var rule = await _db.GraphRules.FirstOrDefaultAsync(r => r.SystemKey == TenureRuleKey, ct);
        if (rule == null || !rule.IsActive)
        {
            return;
        }

        IReadOnlyList<LoyaltyLevelEntry> levels;
        try
        {
            levels = await _levelCatalog.GetLevelsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load loyalty levels for tenure sync.");
            return;
        }

        var snapshot = BuildTenureSnapshot(levels);
        if (TryGetLevelsHash(rule.VariablesJson, out var existingHash) && string.Equals(existingHash, snapshot.LevelsHash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        rule.GraphJson = BuildTenureGraphJson(snapshot);
        rule.VariablesJson = BuildTenureVariablesJson(snapshot, rule.VariablesJson);
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private sealed record TenureSnapshot(string BaseLevel, string LevelsHash, IReadOnlyList<LoyaltyLevelEntry> OrderedLevels);

    private static TenureSnapshot BuildTenureSnapshot(IReadOnlyList<LoyaltyLevelEntry> levels)
    {
        var ordered = levels
            .Where(l => !string.IsNullOrWhiteSpace(l.Key))
            .OrderByDescending(l => l.Years)
            .ToList();

        var baseLevel = levels
            .Where(l => !string.IsNullOrWhiteSpace(l.Key))
            .OrderBy(l => l.Years)
            .FirstOrDefault()?.Key ?? "Base";

        var hashSource = string.Join("|", ordered.Select(l => $"{l.Key}:{l.Years}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSource)));

        return new TenureSnapshot(baseLevel, hash, ordered);
    }

    private static string BuildTenureGraphJson(TenureSnapshot snapshot)
    {
        var nodes = new List<Dictionary<string, object>>
        {
            new()
            {
                ["id"] = "audience",
                ["type"] = "audience_selector",
                ["query"] = new Dictionary<string, object>()
            }
        };

        var edges = new List<Dictionary<string, object>>();

        if (snapshot.OrderedLevels.Count == 0)
        {
            nodes.Add(new Dictionary<string, object>
            {
                ["id"] = "base_set",
                ["type"] = "action_update_profile",
                ["setLevel"] = "var:baseLevel"
            });
            nodes.Add(new Dictionary<string, object> { ["id"] = "end", ["type"] = "end" });
            edges.Add(new Dictionary<string, object> { ["from"] = "audience", ["to"] = "base_set" });
            edges.Add(new Dictionary<string, object> { ["from"] = "base_set", ["to"] = "end" });
        }
        else
        {
            string? prevCheck = null;
            for (var i = 0; i < snapshot.OrderedLevels.Count; i++)
            {
                var checkId = $"level_{i}_check";
                var setId = $"level_{i}_set";

                nodes.Add(new Dictionary<string, object>
                {
                    ["id"] = checkId,
                    ["type"] = "range_switch",
                    ["source"] = "path:user.TenureYears",
                    ["min"] = $"var:level{i}Min"
                });

                nodes.Add(new Dictionary<string, object>
                {
                    ["id"] = setId,
                    ["type"] = "action_update_profile",
                    ["setLevel"] = $"var:level{i}Key"
                });

                if (prevCheck == null)
                {
                    edges.Add(new Dictionary<string, object> { ["from"] = "audience", ["to"] = checkId });
                }
                else
                {
                    edges.Add(new Dictionary<string, object> { ["from"] = prevCheck, ["to"] = checkId, ["when"] = false });
                }

                edges.Add(new Dictionary<string, object> { ["from"] = checkId, ["to"] = setId, ["when"] = true });
                edges.Add(new Dictionary<string, object> { ["from"] = setId, ["to"] = "end" });

                prevCheck = checkId;
            }

            nodes.Add(new Dictionary<string, object>
            {
                ["id"] = "base_set",
                ["type"] = "action_update_profile",
                ["setLevel"] = "var:baseLevel"
            });
            nodes.Add(new Dictionary<string, object> { ["id"] = "end", ["type"] = "end" });
            edges.Add(new Dictionary<string, object> { ["from"] = prevCheck!, ["to"] = "base_set", ["when"] = false });
            edges.Add(new Dictionary<string, object> { ["from"] = "base_set", ["to"] = "end" });
        }

        var graph = new Dictionary<string, object>
        {
            ["start"] = "audience",
            ["nodes"] = nodes,
            ["edges"] = edges
        };

        return JsonSerializer.Serialize(graph);
    }

    private static string BuildTenureVariablesJson(TenureSnapshot snapshot, string? existingVariablesJson)
    {
        var vars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["baseLevel"] = snapshot.BaseLevel,
            ["levelsHash"] = snapshot.LevelsHash
        };

        for (var i = 0; i < snapshot.OrderedLevels.Count; i++)
        {
            vars[$"level{i}Min"] = snapshot.OrderedLevels[i].Years;
            vars[$"level{i}Key"] = snapshot.OrderedLevels[i].Key;
        }

        if (TryGetSchedule(existingVariablesJson, out var schedule))
        {
            vars["schedule"] = schedule;
        }
        else
        {
            vars["schedule"] = new Dictionary<string, object>
            {
                ["kind"] = "daily",
                ["time"] = "00:05",
                ["utcOffsetMinutes"] = 300
            };
        }

        return JsonSerializer.Serialize(vars);
    }

    private static bool TryGetLevelsHash(string? variablesJson, out string? hash)
    {
        hash = null;
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            if (doc.RootElement.TryGetProperty("levelsHash", out var value) && value.ValueKind == JsonValueKind.String)
            {
                hash = value.GetString();
                return !string.IsNullOrWhiteSpace(hash);
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool TryGetSchedule(string? variablesJson, out JsonElement schedule)
    {
        schedule = default;
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            if (doc.RootElement.TryGetProperty("schedule", out var value))
            {
                schedule = value.Clone();
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }
}
