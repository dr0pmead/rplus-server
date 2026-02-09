using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Hunter.API.HeadHunter;
using RPlus.Hunter.API.Persistence;
using RPlus.Hunter.API.Services;
using RPlus.Hunter.API.Waba;
using RPlus.SDK.Hunter.Events;
using RPlus.SDK.Hunter.Models;
using StackExchange.Redis;

namespace RPlus.Hunter.API.Workers;

/// <summary>
/// StalkerWorker — consumes ProfileScoredEvent from Kafka, opens contacts via HH API,
/// and sends WABA template invite IMMEDIATELY (no queue needed with official WABA).
///
/// Pipeline (WABA):
/// 1. Consume ProfileScoredEvent (Passed=true)
/// 2. Check budget fuse (Redis daily counter)
/// 3. Open contact via HH API → save phone to DB IMMEDIATELY
/// 4. Send WABA template invite → status: INVITE_SENT
/// 5. Publish ContactOpenedEvent + save outbound ChatMessage
/// 6. Push via SignalR to admin panel
/// </summary>
public sealed class StalkerWorker : KafkaConsumerBackgroundService<string, ProfileScoredEvent>
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly HeadHunterClient _hhClient;
    private readonly WabaCloudClient _wabaClient;
    private readonly KafkaEventPublisher _eventPublisher;
    private readonly IHubContext<HunterHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly WabaOptions _wabaOptions;

    private const string DailyContactKey = "hunter:daily_contacts";

    public StalkerWorker(
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<StalkerWorker> logger,
        IDbContextFactory<HunterDbContext> dbFactory,
        HeadHunterClient hhClient,
        WabaCloudClient wabaClient,
        KafkaEventPublisher eventPublisher,
        IHubContext<HunterHub> hubContext,
        IConnectionMultiplexer redis,
        IOptions<WabaOptions> wabaOptions)
        : base(kafkaOptions, logger, HunterTopics.ProfilesScored)
    {
        _dbFactory = dbFactory;
        _hhClient = hhClient;
        _wabaClient = wabaClient;
        _eventPublisher = eventPublisher;
        _hubContext = hubContext;
        _redis = redis;
        _wabaOptions = wabaOptions.Value;
    }

    protected override async Task HandleMessageAsync(string key, ProfileScoredEvent scoredEvent, CancellationToken ct)
    {
        if (!scoredEvent.Passed)
        {
            _logger.LogDebug("Profile {ProfileId} rejected (score={Score}), skipping",
                scoredEvent.ProfileId, scoredEvent.Score);
            return;
        }

        _logger.LogInformation("StalkerWorker: processing passed profile {ProfileId} (score={Score})",
            scoredEvent.ProfileId, scoredEvent.Score);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var profile = await db.ParsedProfiles
            .FirstOrDefaultAsync(p => p.Id == scoredEvent.ProfileId, ct);

        if (profile is null)
        {
            _logger.LogWarning("Profile {ProfileId} not found in DB, skipping", scoredEvent.ProfileId);
            return;
        }

        // Idempotency: skip if already processed beyond FilteredOk
        if (profile.Status > ProfileStatus.FilteredOk)
        {
            _logger.LogDebug("Profile {ProfileId} already processed (status={Status}), skipping",
                scoredEvent.ProfileId, profile.Status);
            return;
        }

        // Budget fuse
        if (!await CheckDailyBudgetAsync(scoredEvent.TaskId, ct))
        {
            _logger.LogWarning("Daily contact limit reached for task {TaskId}, pausing", scoredEvent.TaskId);
            await db.SourcingTasks
                .Where(t => t.Id == scoredEvent.TaskId)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(t => t.Status, SourcingTaskStatus.PausedDailyLimit), ct);
            return;
        }

        // ─── CRITICAL: Open contact → save phone IMMEDIATELY ─────────────────
        var contactInfo = await _hhClient.OpenContactAsync(profile.ExternalId, ct);
        if (contactInfo is null)
        {
            _logger.LogWarning("Failed to open contact for {ProfileId}", scoredEvent.ProfileId);
            return;
        }

        var rawPhone = contactInfo.GetPrimaryPhone();
        if (string.IsNullOrEmpty(rawPhone))
        {
            _logger.LogWarning("Profile {ProfileId}: no phone in contact (email: {Email})",
                scoredEvent.ProfileId, contactInfo.Email);
            profile.Status = ProfileStatus.ContactOpened;
            profile.ContactedAt = DateTime.UtcNow;
            profile.ContactEmail = contactInfo.Email;
            await db.SaveChangesAsync(ct);
            return;
        }

        // Save phone immediately (money already spent)
        profile.ContactPhone = rawPhone;
        profile.ContactEmail = contactInfo.Email;
        profile.ContactedAt = DateTime.UtcNow;
        profile.Status = ProfileStatus.ContactOpened;
        await db.SaveChangesAsync(ct);

        // Cross-task dedup: don't invite same person twice
        var alreadyInvited = await db.ParsedProfiles
            .AnyAsync(p => p.ContactPhone == rawPhone
                        && p.Id != profile.Id
                        && p.Status >= ProfileStatus.InviteSent, ct);

        if (alreadyInvited)
        {
            _logger.LogInformation("Phone {Phone} already invited in another task, skipping send", rawPhone);
            await IncrementDailyContactAsync(scoredEvent.TaskId);
            return;
        }

        // ─── SEND WABA TEMPLATE (immediate, no queue) ────────────────────────
        var task = await db.SourcingTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == scoredEvent.TaskId, ct);

        var templateParams = new List<string> { task?.PositionName ?? "специалист" };
        var wabaMessageId = await _wabaClient.SendTemplateAsync(
            rawPhone,
            _wabaOptions.InviteTemplateName,
            templateParams,
            ct);

        if (wabaMessageId is not null)
        {
            // Update status to InviteSent
            profile.Status = ProfileStatus.InviteSent;
            profile.PreferredChannel = OutreachChannel.WhatsApp;

            // Save outbound message to chat history
            var chatMsg = new ChatMessage
            {
                ProfileId = profile.Id,
                Direction = MessageDirection.Outbound,
                SenderType = Waba.SenderType.Ai,
                Content = $"[Шаблон: {_wabaOptions.InviteTemplateName}] {task?.PositionName ?? "вакансия"}",
                WabaMessageId = wabaMessageId,
                Status = "sent"
            };
            db.ChatMessages.Add(chatMsg);
            await db.SaveChangesAsync(ct);

            // Update task stats
            await db.SourcingTasks
                .Where(t => t.Id == scoredEvent.TaskId)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(t => t.CandidatesContacted, t => t.CandidatesContacted + 1), ct);

            // Push to SignalR
            var dto = new ChatMessageDto
            {
                Id = chatMsg.Id,
                ProfileId = profile.Id,
                Direction = chatMsg.Direction,
                SenderType = chatMsg.SenderType,
                Content = chatMsg.Content,
                WabaMessageId = chatMsg.WabaMessageId,
                Status = chatMsg.Status,
                CreatedAt = chatMsg.CreatedAt
            };
            await _hubContext.Clients.Group($"task:{scoredEvent.TaskId}").SendAsync("NewMessage", dto, ct);

            _logger.LogInformation(
                "WABA invite sent to profile {ProfileId}: phone={Phone}, wabaId={WabaId}",
                scoredEvent.ProfileId, rawPhone, wabaMessageId);
        }
        else
        {
            _logger.LogWarning("WABA send failed for {ProfileId}, contact saved (phone={Phone})",
                scoredEvent.ProfileId, rawPhone);
        }

        await IncrementDailyContactAsync(scoredEvent.TaskId);

        // Publish Kafka event
        await _eventPublisher.PublishAsync(
            new ContactOpenedEvent
            {
                ProfileId = scoredEvent.ProfileId,
                TaskId = scoredEvent.TaskId,
                Phone = rawPhone,
                TelegramHandle = profile.TelegramHandle
            },
            HunterTopics.ContactOpened,
            scoredEvent.TaskId.ToString(),
            ct);
    }

    private async Task<bool> CheckDailyBudgetAsync(Guid taskId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var task = await db.SourcingTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        if (task is null) return false;

        var todayKey = $"{DailyContactKey}:{taskId}:{DateTime.UtcNow:yyyy-MM-dd}";
        var todayCount = (long)(await _redis.GetDatabase().StringGetAsync(todayKey));
        return todayCount < task.DailyContactLimit;
    }

    private async Task IncrementDailyContactAsync(Guid taskId)
    {
        var todayKey = $"{DailyContactKey}:{taskId}:{DateTime.UtcNow:yyyy-MM-dd}";
        var redisDb = _redis.GetDatabase();
        await redisDb.StringIncrementAsync(todayKey);
        await redisDb.KeyExpireAsync(todayKey, TimeSpan.FromHours(25));
    }
}
