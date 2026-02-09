using Microsoft.AspNetCore.SignalR;

namespace RPlus.Hunter.API.Waba;

/// <summary>
/// SignalR Hub for real-time Hunter chat updates.
/// Pushes inbound messages, status changes, and AI responses to admin panel.
///
/// Groups:
///   - "task:{taskId}" — all profiles for a given sourcing task
///   - "profile:{profileId}" — specific candidate chat
/// </summary>
public sealed class HunterHub : Hub
{
    private readonly ILogger<HunterHub> _logger;

    public HunterHub(ILogger<HunterHub> logger) => _logger = logger;

    /// <summary>
    /// Client joins a task group to receive updates for all candidates in that task.
    /// </summary>
    public async Task JoinTaskGroup(string taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"task:{taskId}");
        _logger.LogDebug("Client {ConnectionId} joined task:{TaskId}", Context.ConnectionId, taskId);
    }

    /// <summary>
    /// Client joins a specific profile chat to receive real-time messages.
    /// </summary>
    public async Task JoinProfileChat(string profileId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"profile:{profileId}");
        _logger.LogDebug("Client {ConnectionId} joined profile:{ProfileId}", Context.ConnectionId, profileId);
    }

    /// <summary>
    /// Client leaves a task group.
    /// </summary>
    public async Task LeaveTaskGroup(string taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"task:{taskId}");
    }

    /// <summary>
    /// Client leaves a profile chat.
    /// </summary>
    public async Task LeaveProfileChat(string profileId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"profile:{profileId}");
    }

    /// <summary>
    /// HR sends a manual message to a candidate (switches conversation_mode to HUMAN_MANUAL).
    /// </summary>
    public async Task SendManualMessage(string profileId, string message)
    {
        _logger.LogInformation("Manual message from HR to profile {ProfileId}: {Message}",
            profileId, message[..Math.Min(message.Length, 100)]);

        // Actual sending is handled by the webhook controller / service layer
        // This just signals intent — the server-side handler will:
        // 1. Switch conversation_mode to HUMAN_MANUAL
        // 2. Send via WABA
        // 3. Save to chat_messages
        // 4. Push confirmation back via this hub
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("HunterHub client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("HunterHub client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// SignalR event DTOs pushed to clients.
/// </summary>
public sealed record ChatMessageDto
{
    public required Guid Id { get; init; }
    public required Guid ProfileId { get; init; }
    public required string Direction { get; init; }
    public required string SenderType { get; init; }
    public string? Content { get; init; }
    public string? WabaMessageId { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record MessageStatusDto
{
    public required string WabaMessageId { get; init; }
    public required string Status { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
