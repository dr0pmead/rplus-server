using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Hunter.API.Persistence;
using RPlus.Hunter.API.Services;
using RPlus.SDK.Hunter.Models;

namespace RPlus.Hunter.API.Waba;

/// <summary>
/// Webhook controller for inbound WhatsApp messages and delivery status updates.
/// 
/// Security: Every POST is validated with HMAC-SHA256 signature (X-Hub-Signature-256).
/// Without this, anyone can send fake "Я согласен!" messages to our admin panel.
///
/// Meta Cloud API sends POST to this endpoint when:
/// - Candidate sends a message (inbound)
/// - Message status changes (sent → delivered → read)
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/hunter/webhook/whatsapp")]
public sealed class WhatsAppWebhookController : ControllerBase
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly IHubContext<HunterHub> _hubContext;
    private readonly WabaCloudClient _wabaClient;
    private readonly WabaSignatureValidator _signatureValidator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WabaOptions _options;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IDbContextFactory<HunterDbContext> dbFactory,
        IHubContext<HunterHub> hubContext,
        WabaCloudClient wabaClient,
        WabaSignatureValidator signatureValidator,
        IServiceScopeFactory scopeFactory,
        IOptions<WabaOptions> options,
        ILogger<WhatsAppWebhookController> logger)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
        _wabaClient = wabaClient;
        _signatureValidator = signatureValidator;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Webhook verification (GET) — Meta sends this to verify endpoint ownership.
    /// </summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" && verifyToken == _options.WebhookVerifyToken)
        {
            _logger.LogInformation("Webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed: mode={Mode}, token mismatch", mode);
        return StatusCode(403, "Verification failed");
    }

    /// <summary>
    /// Receive inbound messages and status updates from Meta Cloud API.
    /// SECURITY: Validates X-Hub-Signature-256 HMAC before processing.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // ─── HMAC-SHA256 Signature Verification ──────────────────────────
        // Read raw body for signature check (cannot use [FromBody] here — 
        // we need raw bytes before deserialization).
        var bodyBytes = await ReadBodyAsync(ct);
        var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        if (!_signatureValidator.Validate(signatureHeader, bodyBytes))
        {
            _logger.LogWarning("Webhook rejected: invalid HMAC signature from {IP}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid signature" });
        }

        // ─── Deserialize nested Meta Cloud API payload ───────────────────
        MetaWebhookPayload? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<MetaWebhookPayload>(bodyBytes);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Webhook: invalid JSON payload");
            return BadRequest(new { error = "Invalid JSON" });
        }

        if (payload?.Entry is null)
            return Ok(); // Empty payload, nothing to process

        // ─── Flatten nested structure and process ────────────────────────
        foreach (var entry in payload.Entry)
        {
            if (entry.Changes is null) continue;

            foreach (var change in entry.Changes)
            {
                var value = change.Value;
                if (value is null) continue;

                // Process inbound messages
                if (value.Messages is { Count: > 0 })
                {
                    foreach (var msg in value.Messages)
                        await HandleInboundMessageAsync(msg, ct);
                }

                // Process status updates (delivered, read, failed)
                if (value.Statuses is { Count: > 0 })
                {
                    foreach (var status in value.Statuses)
                        await HandleStatusUpdateAsync(status, ct);
                }
            }
        }

        // Always return 200 to prevent Meta from retrying
        return Ok();
    }

    // ─── Inbound Message Handler ─────────────────────────────────────────────

    private async Task HandleInboundMessageAsync(WabaInboundMessage msg, CancellationToken ct)
    {
        var senderPhone = msg.From;
        var messageText = msg.Text?.Body;

        if (string.IsNullOrEmpty(messageText))
        {
            _logger.LogDebug("Inbound non-text message from {Phone} (type={Type}), skipping",
                senderPhone, msg.Type);
            return;
        }

        _logger.LogInformation("Inbound WhatsApp from {Phone}: {Text}",
            senderPhone, messageText[..Math.Min(messageText.Length, 100)]);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Find profile by phone (uses idx_parsed_profiles_contact_phone index)
        var profile = await db.ParsedProfiles
            .FirstOrDefaultAsync(p => p.ContactPhone == senderPhone, ct);

        if (profile is null)
        {
            _logger.LogWarning("Inbound message from unknown phone {Phone}, ignoring", senderPhone);
            return;
        }

        // Idempotency: skip if we already have this message
        var duplicate = await db.ChatMessages
            .AnyAsync(m => m.WabaMessageId == msg.Id, ct);
        if (duplicate)
        {
            _logger.LogDebug("Duplicate inbound message {MessageId}, ignoring", msg.Id);
            return;
        }

        // Save inbound message
        var chatMessage = new ChatMessage
        {
            ProfileId = profile.Id,
            Direction = MessageDirection.Inbound,
            SenderType = Waba.SenderType.Candidate,
            Content = messageText,
            WabaMessageId = msg.Id,
            Status = "received"
        };
        db.ChatMessages.Add(chatMessage);

        // Update profile status if first response
        if (profile.Status == ProfileStatus.InviteSent)
        {
            profile.Status = ProfileStatus.Responded;
            profile.RespondedAt = DateTime.UtcNow;

            // Increment task response counter
            await db.SourcingTasks
                .Where(t => t.Id == profile.TaskId)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(t => t.CandidatesResponded, t => t.CandidatesResponded + 1), ct);
        }

        await db.SaveChangesAsync(ct);

        // Push to SignalR — both profile chat and task group
        var dto = MapToDto(chatMessage);

        await _hubContext.Clients.Group($"profile:{profile.Id}").SendAsync("NewMessage", dto, ct);
        await _hubContext.Clients.Group($"task:{profile.TaskId}").SendAsync("NewMessage", dto, ct);

        _logger.LogInformation("Inbound message saved for profile {ProfileId}, pushed to SignalR", profile.Id);

        // If in AI_AUTO mode, generate AI response.
        // Fire-and-forget: dispatch to background so webhook returns 200 immediately.
        // Meta expects response within 15-20s; AI generation may take longer.
        if (profile.ConversationMode == ConversationMode.AiAuto)
        {
            var pId = profile.Id;
            var tId = profile.TaskId;
            var phone = profile.ContactPhone!;
            var text = messageText;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Create a fresh DI scope for the background task.
                    // The HTTP request scope is already disposed by now —
                    // reusing scoped services would cause ObjectDisposedException.
                    using var scope = _scopeFactory.CreateScope();
                    var recruiter = scope.ServiceProvider.GetRequiredService<AiRecruiterService>();

                    await recruiter.HandleInboundAsync(pId, tId, phone, text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "AI_AUTO background task failed for profile {ProfileId}", pId);
                }
            });
        }
    }

    // ─── Status Update Handler ───────────────────────────────────────────────

    /// <summary>
    /// Handles delivery status updates: sent → delivered → read → failed.
    /// Only upgrades status (prevents "read" from being overwritten by late "delivered").
    /// </summary>
    private async Task HandleStatusUpdateAsync(WabaStatusUpdate status, CancellationToken ct)
    {
        _logger.LogDebug("WABA status update: {MessageId} → {Status}", status.Id, status.Status);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chatMsg = await db.ChatMessages
            .FirstOrDefaultAsync(m => m.WabaMessageId == status.Id, ct);

        if (chatMsg is null)
        {
            _logger.LogDebug("Status update for unknown message {MessageId}", status.Id);
            return;
        }

        // Only upgrade status: sent → delivered → read. Never downgrade.
        var statusRank = GetStatusRank(status.Status);
        var currentRank = GetStatusRank(chatMsg.Status);

        if (statusRank <= currentRank)
        {
            _logger.LogDebug("Status {New} is not an upgrade from {Current} for {MessageId}",
                status.Status, chatMsg.Status, status.Id);
            return;
        }

        chatMsg.Status = status.Status;
        await db.SaveChangesAsync(ct);

        // Push status update to SignalR
        var statusDto = new MessageStatusDto
        {
            WabaMessageId = status.Id,
            Status = status.Status,
            UpdatedAt = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"profile:{chatMsg.ProfileId}")
            .SendAsync("MessageStatus", statusDto, ct);

        _logger.LogDebug("Status updated: {MessageId} → {Status}", status.Id, status.Status);
    }

    // ─── Manual Send (HR → Candidate) ────────────────────────────────────────

    /// <summary>
    /// HR sends a manual message to a candidate.
    /// POST /api/hunter/webhook/whatsapp/send
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendManualMessage(
        [FromBody] ManualMessageRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty" });

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var profile = await db.ParsedProfiles
            .FirstOrDefaultAsync(p => p.Id == request.ProfileId, ct);

        if (profile is null)
            return NotFound(new { error = "Profile not found" });

        if (string.IsNullOrEmpty(profile.ContactPhone))
            return BadRequest(new { error = "Profile has no phone number" });

        // Switch to manual mode
        profile.ConversationMode = ConversationMode.HumanManual;

        // Send via WABA
        var messageId = await _wabaClient.SendTextAsync(profile.ContactPhone, request.Message, ct);

        if (messageId is null)
            return StatusCode(502, new { error = "Failed to send message via WABA" });

        // Save outbound message
        var chatMessage = new ChatMessage
        {
            ProfileId = profile.Id,
            Direction = MessageDirection.Outbound,
            SenderType = Waba.SenderType.Human,
            Content = request.Message,
            WabaMessageId = messageId,
            Status = "sent"
        };
        db.ChatMessages.Add(chatMessage);
        await db.SaveChangesAsync(ct);

        // Push to SignalR
        var dto = MapToDto(chatMessage);
        await _hubContext.Clients.Group($"profile:{profile.Id}").SendAsync("NewMessage", dto, ct);
        await _hubContext.Clients.Group($"task:{profile.TaskId}").SendAsync("NewMessage", dto, ct);

        return Ok(new { messageId = chatMessage.Id, wabaMessageId = messageId });
    }

    // ─── Conversation Mode Switch ────────────────────────────────────────────

    /// <summary>
    /// Switch conversation mode for a profile.
    /// POST /api/hunter/profiles/{profileId}/mode
    /// </summary>
    [HttpPost("/api/hunter/profiles/{profileId}/mode")]
    public async Task<IActionResult> SwitchConversationMode(
        Guid profileId,
        [FromBody] ConversationModeRequest request,
        CancellationToken ct)
    {
        if (request.Mode is not (ConversationMode.AiAuto or ConversationMode.HumanManual))
            return BadRequest(new { error = $"Invalid mode. Use '{ConversationMode.AiAuto}' or '{ConversationMode.HumanManual}'" });

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var updated = await db.ParsedProfiles
            .Where(p => p.Id == profileId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(p => p.ConversationMode, request.Mode), ct);

        if (updated == 0)
            return NotFound(new { error = "Profile not found" });

        _logger.LogInformation("Profile {ProfileId} conversation mode → {Mode}", profileId, request.Mode);

        // Notify SignalR subscribers
        await _hubContext.Clients.Group($"profile:{profileId}")
            .SendAsync("ModeChanged", new { profileId, mode = request.Mode }, ct);

        return Ok(new { profileId, mode = request.Mode });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Status rank for monotonic upgrade logic.
    /// Prevents "read" from being overwritten by a late-arriving "delivered" event.
    /// </summary>
    private static int GetStatusRank(string status) => status switch
    {
        "sent" => 1,
        "delivered" => 2,
        "read" => 3,
        "failed" => 0,      // Failed is a terminal state, treat as lowest
        "received" => 1,    // Inbound received
        _ => 0
    };

    private static ChatMessageDto MapToDto(ChatMessage msg) => new()
    {
        Id = msg.Id,
        ProfileId = msg.ProfileId,
        Direction = msg.Direction,
        SenderType = msg.SenderType,
        Content = msg.Content,
        WabaMessageId = msg.WabaMessageId,
        Status = msg.Status,
        CreatedAt = msg.CreatedAt
    };

    private async Task<byte[]> ReadBodyAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}

public sealed record ManualMessageRequest
{
    public Guid ProfileId { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record ConversationModeRequest
{
    public string Mode { get; init; } = ConversationMode.AiAuto;
}
