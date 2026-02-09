namespace RPlus.SDK.AI;

/// <summary>
/// WebSocket realtime hub for AI streaming.
/// </summary>
public interface IAIRealtimeHub
{
    /// <summary>
    /// Send streaming token to client.
    /// </summary>
    Task SendTokenAsync(
        string connectionId,
        Guid conversationId,
        string token,
        CancellationToken ct = default);

    /// <summary>
    /// Send thinking status to client.
    /// </summary>
    Task SendThinkingAsync(
        string connectionId,
        Guid conversationId,
        string status,
        CancellationToken ct = default);

    /// <summary>
    /// Send tool execution notification.
    /// </summary>
    Task SendToolUseAsync(
        string connectionId,
        Guid conversationId,
        string toolName,
        string arguments,
        CancellationToken ct = default);

    /// <summary>
    /// Send completion notification.
    /// </summary>
    Task SendCompleteAsync(
        string connectionId,
        Guid conversationId,
        RealtimeCompletionStats stats,
        CancellationToken ct = default);

    /// <summary>
    /// Send error notification.
    /// </summary>
    Task SendErrorAsync(
        string connectionId,
        Guid conversationId,
        string error,
        CancellationToken ct = default);

    /// <summary>
    /// Broadcast to all subscribers of a conversation.
    /// </summary>
    Task BroadcastToConversationAsync(
        Guid conversationId,
        string eventType,
        object data,
        CancellationToken ct = default);
}

/// <summary>
/// Completion stats for realtime.
/// </summary>
public sealed record RealtimeCompletionStats(
    long ProcessingTimeMs,
    long TimeToFirstTokenMs,
    int TokenCount);

/// <summary>
/// Realtime message types.
/// </summary>
public static class RealtimeMessageTypes
{
    public const string Token = "ai.token";
    public const string Thinking = "ai.thinking";
    public const string ToolUse = "ai.tool_use";
    public const string Complete = "ai.complete";
    public const string Error = "ai.error";
    public const string RateLimited = "ai.rate_limited";
}
