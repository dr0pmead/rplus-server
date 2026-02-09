using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RPlus.Hunter.API.Persistence;
using RPlus.Hunter.API.Waba;

namespace RPlus.Hunter.API.Services;

/// <summary>
/// Orchestrator for AI_AUTO conversations.
/// Connects inbound WhatsApp messages → AiBrainService → outbound WABA response.
///
/// This is the "nerve" that closes the Webhook → AI → WABA loop.
///
/// Design decisions:
///   - Stateless: loads chat history from DB on every call (no in-memory state).
///   - Fire-and-forget: called from Task.Run in webhook controller (with fresh DI scope).
///   - Fail-safe: all exceptions are caught and logged, never re-thrown.
///   - Delegates AI logic to AiBrainService (RAG, MCP, DeepSeek R1).
/// </summary>
public sealed class AiRecruiterService
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly AiBrainService _brain;
    private readonly WabaCloudClient _wabaClient;
    private readonly IHubContext<HunterHub> _hubContext;
    private readonly ILogger<AiRecruiterService> _logger;

    /// <summary>
    /// Maximum number of historical messages to include in AI context.
    /// Keeps token usage reasonable while providing enough conversational context.
    /// </summary>
    private const int MaxHistoryMessages = 20;

    public AiRecruiterService(
        IDbContextFactory<HunterDbContext> dbFactory,
        AiBrainService brain,
        WabaCloudClient wabaClient,
        IHubContext<HunterHub> hubContext,
        ILogger<AiRecruiterService> logger)
    {
        _dbFactory = dbFactory;
        _brain = brain;
        _wabaClient = wabaClient;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Handles an inbound candidate message when profile is in AI_AUTO mode.
    ///
    /// Flow:
    ///   1. Load chat history from DB
    ///   2. Build conversation messages
    ///   3. Delegate to AiBrainService (RAG + DeepSeek R1 + thought cleaning)
    ///   4. Send AI response via WABA
    ///   5. Save outbound message to DB
    ///   6. Push to SignalR
    /// </summary>
    public async Task HandleInboundAsync(
        Guid profileId,
        Guid taskId,
        string phone,
        string inboundText,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AiRecruiter processing inbound for profile {ProfileId}: {Text}",
            profileId, inboundText[..Math.Min(inboundText.Length, 80)]);

        try
        {
            // ── Step 1: Load chat history ───────────────────────────────────
            var history = await LoadChatHistoryAsync(profileId, ct);

            // ── Step 2: Build conversation messages ─────────────────────────
            var messages = BuildConversationMessages(history, inboundText);

            // ── Step 3: Generate AI response via Brain service ──────────────
            var aiResponse = await _brain.GenerateResponseAsync(profileId, messages, ct);

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                _logger.LogWarning("AI returned empty response for profile {ProfileId}", profileId);
                return;
            }

            _logger.LogInformation(
                "AI response for profile {ProfileId}: {Response}",
                profileId, aiResponse[..Math.Min(aiResponse.Length, 100)]);

            // ── Step 4: Send via WABA ───────────────────────────────────────
            var wabaMessageId = await _wabaClient.SendTextAsync(phone, aiResponse, ct);

            if (wabaMessageId is null)
            {
                _logger.LogError("WABA send failed for profile {ProfileId} — AI response lost", profileId);
                return;
            }

            // ── Step 5: Save outbound AI message to DB ──────────────────────
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var outboundMessage = new ChatMessage
            {
                ProfileId = profileId,
                Direction = MessageDirection.Outbound,
                SenderType = SenderType.Ai,
                Content = aiResponse,
                WabaMessageId = wabaMessageId,
                Status = "sent"
            };
            db.ChatMessages.Add(outboundMessage);
            await db.SaveChangesAsync(ct);

            // ── Step 6: Push to SignalR ──────────────────────────────────────
            var dto = new ChatMessageDto
            {
                Id = outboundMessage.Id,
                ProfileId = profileId,
                Direction = MessageDirection.Outbound,
                SenderType = SenderType.Ai,
                Content = aiResponse,
                WabaMessageId = wabaMessageId,
                Status = "sent",
                CreatedAt = outboundMessage.CreatedAt
            };

            await _hubContext.Clients.Group($"profile:{profileId}").SendAsync("NewMessage", dto, ct);
            await _hubContext.Clients.Group($"task:{taskId}").SendAsync("NewMessage", dto, ct);

            _logger.LogInformation(
                "AiRecruiter completed for profile {ProfileId}: sent via WABA, pushed to SignalR",
                profileId);
        }
        catch (Exception ex)
        {
            // Fail-safe: never let an exception escape.
            // HR can always switch to HUMAN_MANUAL if AI fails.
            _logger.LogError(ex,
                "AiRecruiter failed for profile {ProfileId}. Candidate will not receive AI response",
                profileId);
        }
    }

    // ─── Private Helpers ────────────────────────────────────────────────────

    private async Task<List<ChatMessage>> LoadChatHistoryAsync(Guid profileId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ChatMessages
            .Where(m => m.ProfileId == profileId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    private static List<ConversationMessage> BuildConversationMessages(
        List<ChatMessage> history,
        string latestInbound)
    {
        var messages = new List<ConversationMessage>
        {
            // System prompt will be replaced by AiBrainService with RAG context
            new("system", "")
        };

        foreach (var msg in history)
        {
            if (string.IsNullOrEmpty(msg.Content)) continue;

            var role = msg.Direction == MessageDirection.Outbound ? "assistant" : "user";
            messages.Add(new ConversationMessage(role, msg.Content));
        }

        // Avoid duplicating the latest inbound if it's already in history
        var lastMsg = messages.LastOrDefault();
        if (lastMsg is null || lastMsg.Role != "user" || lastMsg.Content != latestInbound)
        {
            messages.Add(new ConversationMessage("user", latestInbound));
        }

        return messages;
    }
}
