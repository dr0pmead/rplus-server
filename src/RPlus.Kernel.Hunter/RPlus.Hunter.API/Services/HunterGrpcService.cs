using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using RPlus.Hunter.API.Persistence;
using RPlus.SDK.Hunter.Events;
using RPlus.SDK.Hunter.Models;
using RPlusGrpc.Hunter;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RPlus.Hunter.API.Services;

/// <summary>
/// gRPC service for Hunter task management.
/// No auth — Gateway-Only Authorization standard.
/// </summary>
public sealed class HunterGrpcService : HunterService.HunterServiceBase
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly ILogger<HunterGrpcService> _logger;
    private readonly KafkaEventPublisher _eventPublisher;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;

    public HunterGrpcService(
        IDbContextFactory<HunterDbContext> dbFactory,
        ILogger<HunterGrpcService> logger,
        KafkaEventPublisher eventPublisher,
        StackExchange.Redis.IConnectionMultiplexer redis)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _eventPublisher = eventPublisher;
        _redis = redis;
    }

    public override async Task<CreateTaskResponse> CreateTask(CreateTaskRequest request, ServerCallContext context)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(context.CancellationToken);

        var entity = new SourcingTaskEntity
        {
            PositionName = request.PositionName,
            SearchQuery = request.SearchQuery,
            Conditions = request.Conditions,
            MessageTemplate = string.IsNullOrEmpty(request.MessageTemplate) ? null : request.MessageTemplate,
            DailyContactLimit = request.DailyContactLimit > 0 ? request.DailyContactLimit : 50,
            MinScore = request.MinScore > 0 ? request.MinScore : 70,
            CreatedByUserId = Guid.TryParse(request.CreatedByUserId, out var uid) ? uid : Guid.Empty,
            Status = SourcingTaskStatus.Active
        };

        db.SourcingTasks.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        // Publish event for HarvesterWorker
        await _eventPublisher.PublishAsync(
            new TaskCreatedEvent
            {
                TaskId = entity.Id,
                PositionName = entity.PositionName,
                SearchQuery = entity.SearchQuery,
                Conditions = entity.Conditions,
                MinScore = entity.MinScore,
                CreatedByUserId = entity.CreatedByUserId
            },
            HunterTopics.TaskCreated,
            entity.Id.ToString(),
            context.CancellationToken);

        _logger.LogInformation("Created sourcing task {TaskId}: {Position}", entity.Id, entity.PositionName);

        return new CreateTaskResponse { TaskId = entity.Id.ToString() };
    }

    public override async Task<TaskResponse> GetTask(GetTaskRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TaskId, out var taskId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid task_id"));

        await using var db = await _dbFactory.CreateDbContextAsync(context.CancellationToken);
        var task = await db.SourcingTasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, context.CancellationToken);

        if (task is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Task not found"));

        return MapToResponse(task);
    }

    public override async Task<ListTasksResponse> ListTasks(ListTasksRequest request, ServerCallContext context)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(context.CancellationToken);

        var query = db.SourcingTasks.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(request.StatusFilter) &&
            System.Enum.TryParse<SourcingTaskStatus>(request.StatusFilter, true, out var statusFilter))
        {
            query = query.Where(t => t.Status == statusFilter);
        }

        var totalCount = await query.CountAsync(context.CancellationToken);
        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, 100) : 20;
        var page = request.Page > 0 ? request.Page : 1;

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListTasksResponse { TotalCount = totalCount };
        response.Tasks.AddRange(tasks.Select(MapToResponse));
        return response;
    }

    public override async Task<TaskResponse> UpdateTaskStatus(UpdateTaskStatusRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TaskId, out var taskId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid task_id"));

        if (!System.Enum.TryParse<SourcingTaskStatus>(request.Status, true, out var newStatus))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid status"));

        await using var db = await _dbFactory.CreateDbContextAsync(context.CancellationToken);
        var task = await db.SourcingTasks.FirstOrDefaultAsync(t => t.Id == taskId, context.CancellationToken);

        if (task is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Task not found"));

        task.Status = newStatus;
        if (newStatus == SourcingTaskStatus.Completed)
            task.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Task {TaskId} status updated to {Status}", taskId, newStatus);
        return MapToResponse(task);
    }

    public override async Task<InjectProfileResponse> InjectProfile(InjectProfileRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TaskId, out var taskId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid task_id"));

        if (string.IsNullOrWhiteSpace(request.RawData))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "raw_data is required"));

        await using var db = await _dbFactory.CreateDbContextAsync(context.CancellationToken);

        var task = await db.SourcingTasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, context.CancellationToken);

        if (task is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Task not found"));

        var externalId = string.IsNullOrEmpty(request.ExternalId)
            ? $"manual-{Guid.NewGuid():N}"
            : request.ExternalId;

        var contentHash = ComputeHash(request.RawData);

        // Smart dedup: check if already exists with same content
        var existing = await db.ParsedProfiles
            .FirstOrDefaultAsync(p => p.TaskId == taskId && p.ExternalId == externalId, context.CancellationToken);

        if (existing is not null)
        {
            if (existing.ContentHash == contentHash)
                return new InjectProfileResponse { ProfileId = existing.Id.ToString(), IsDuplicate = true };

            // Content changed — update and re-score
            existing.RawData = request.RawData;
            existing.ContentHash = contentHash;
            existing.Status = ProfileStatus.New;
            existing.AiScore = null;
            existing.AiVerdict = null;
            await db.SaveChangesAsync(context.CancellationToken);
        }
        else
        {
            // Detect Telegram handle in raw data
            var tgHandle = ExtractTelegramHandle(request.RawData);

            existing = new ParsedProfileEntity
            {
                TaskId = taskId,
                ExternalId = externalId,
                Source = string.IsNullOrEmpty(request.Source) ? "manual" : request.Source,
                RawData = request.RawData,
                ContentHash = contentHash,
                TelegramHandle = tgHandle,
                PreferredChannel = !string.IsNullOrEmpty(tgHandle) ? OutreachChannel.Telegram : OutreachChannel.WhatsApp
            };

            db.ParsedProfiles.Add(existing);
            await db.SaveChangesAsync(context.CancellationToken);

            // Update counter
            await db.SourcingTasks
                .Where(t => t.Id == taskId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CandidatesFound, t => t.CandidatesFound + 1),
                    context.CancellationToken);
        }

        // Publish for JudgeWorker
        await _eventPublisher.PublishAsync(
            new ProfileParsedEvent
            {
                ProfileId = existing.Id,
                TaskId = taskId,
                ExternalId = externalId,
                RawData = request.RawData,
                ContentHash = contentHash,
                Conditions = task.Conditions,
                MinScore = task.MinScore
            },
            HunterTopics.ProfilesParsed,
            taskId.ToString(),
            context.CancellationToken);

        return new InjectProfileResponse { ProfileId = existing.Id.ToString(), IsDuplicate = false };
    }

    public override async Task<TaskStatsResponse> GetTaskStats(GetTaskStatsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TaskId, out var taskId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid task_id"));

        await using var db = await _dbFactory.CreateDbContextAsync(context.CancellationToken);

        var stats = await db.ParsedProfiles
            .Where(p => p.TaskId == taskId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(p => p.Status == ProfileStatus.New),
                Passed = g.Count(p => p.Status == ProfileStatus.FilteredOk || p.Status == ProfileStatus.ContactOpened || p.Status == ProfileStatus.InviteSent),
                Rejected = g.Count(p => p.Status == ProfileStatus.Rejected),
                ContactsOpened = g.Count(p => p.Status == ProfileStatus.ContactOpened || p.Status == ProfileStatus.InviteSent),
                MessagesSent = g.Count(p => p.Status == ProfileStatus.InviteSent),
                Responses = g.Count(p => p.Status == ProfileStatus.Responded || p.Status == ProfileStatus.Qualified)
            })
            .FirstOrDefaultAsync(context.CancellationToken);

        // Daily contacts remaining from Redis
        var redisDb = _redis.GetDatabase();
        var dailyKey = $"hunter:daily_contacts:{taskId}:{DateTime.UtcNow:yyyy-MM-dd}";
        var dailyUsed = (int)(await redisDb.StringGetAsync(dailyKey));

        var task = await db.SourcingTasks.AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => t.DailyContactLimit)
            .FirstOrDefaultAsync(context.CancellationToken);

        return new TaskStatsResponse
        {
            TotalParsed = stats?.Total ?? 0,
            PendingScoring = stats?.Pending ?? 0,
            Passed = stats?.Passed ?? 0,
            Rejected = stats?.Rejected ?? 0,
            ContactsOpened = stats?.ContactsOpened ?? 0,
            MessagesSent = stats?.MessagesSent ?? 0,
            ResponsesReceived = stats?.Responses ?? 0,
            DailyContactsRemaining = Math.Max(0, task - dailyUsed)
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static TaskResponse MapToResponse(SourcingTaskEntity task) => new()
    {
        Id = task.Id.ToString(),
        PositionName = task.PositionName,
        SearchQuery = task.SearchQuery,
        Conditions = task.Conditions,
        MessageTemplate = task.MessageTemplate ?? "",
        DailyContactLimit = task.DailyContactLimit,
        MinScore = task.MinScore,
        Status = task.Status.ToString(),
        CandidatesFound = task.CandidatesFound,
        CandidatesContacted = task.CandidatesContacted,
        CandidatesResponded = task.CandidatesResponded,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(task.CreatedAt, DateTimeKind.Utc)),
        CreatedByUserId = task.CreatedByUserId.ToString()
    };

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Extracts Telegram handle from resume text (t.me/username or @username).
    /// </summary>
    private static string? ExtractTelegramHandle(string text)
    {
        // Match t.me/username patterns
        var tmeMatch = System.Text.RegularExpressions.Regex.Match(
            text, @"t\.me/([a-zA-Z0-9_]{5,32})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (tmeMatch.Success)
            return "@" + tmeMatch.Groups[1].Value;

        // Match @username patterns (not emails)
        var atMatch = System.Text.RegularExpressions.Regex.Match(
            text, @"(?<!\S)@([a-zA-Z][a-zA-Z0-9_]{4,31})(?!\S)");
        if (atMatch.Success && !text.Contains(atMatch.Value + "."))
            return atMatch.Value;

        return null;
    }
}
