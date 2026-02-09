namespace RPlus.SDK.AI;

/// <summary>
/// Short-term conversation memory.
/// </summary>
public interface IConversationMemory
{
    /// <summary>
    /// Get conversation context.
    /// </summary>
    Task<ConversationContext?> GetAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Save or update conversation context.
    /// </summary>
    Task SaveAsync(ConversationContext context, CancellationToken ct = default);

    /// <summary>
    /// Append a message to conversation history.
    /// </summary>
    Task AppendMessageAsync(
        Guid conversationId,
        LlmMessage message,
        CancellationToken ct = default);

    /// <summary>
    /// Get recent messages (for context window).
    /// </summary>
    Task<IReadOnlyList<LlmMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int count = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Delete conversation.
    /// </summary>
    Task DeleteAsync(Guid conversationId, CancellationToken ct = default);
}

/// <summary>
/// Conversation context holding state and metadata.
/// </summary>
public sealed record ConversationContext
{
    public required Guid ConversationId { get; init; }
    public Guid? UserId { get; init; }
    public required string Domain { get; init; }
    public Guid? InstructionId { get; init; }
    public string? AgentId { get; init; }
    public Dictionary<string, object> State { get; init; } = [];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; init; } = DateTime.UtcNow;
}
