namespace RPlus.SDK.AI;

/// <summary>
/// AI event types for Kafka publishing.
/// </summary>
public static class AIEventTypes
{
    public const string ConversationCreated = "ai.conversation.created.v1";
    public const string ConversationUpdated = "ai.conversation.updated.v1";
    public const string MessageSent = "ai.message.sent.v1";
    public const string MessageReceived = "ai.message.received.v1";
    public const string ToolExecuted = "ai.tool.executed.v1";
    public const string RagQueryPerformed = "ai.rag.query.v1";
    public const string DocumentIndexed = "ai.document.indexed.v1";
}

/// <summary>
/// Base AI event.
/// </summary>
public abstract record AIEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}

/// <summary>
/// Conversation created event.
/// </summary>
public sealed record ConversationCreatedEvent : AIEvent
{
    public override string EventType => AIEventTypes.ConversationCreated;
    public required Guid ConversationId { get; init; }
    public Guid? UserId { get; init; }
    public required string Domain { get; init; }
    public string? AgentId { get; init; }
}

/// <summary>
/// Message sent/received event.
/// </summary>
public sealed record MessageEvent : AIEvent
{
    private readonly string _eventType;
    public override string EventType => _eventType;
    public required Guid ConversationId { get; init; }
    public Guid? UserId { get; init; }
    public required LlmRole Role { get; init; }
    public required string Content { get; init; }
    public int? TokenCount { get; init; }
    public long? ProcessingTimeMs { get; init; }

    public MessageEvent() : this(AIEventTypes.MessageSent) { }
    public MessageEvent(string eventType) => _eventType = eventType;

    public static MessageEvent Sent(Guid conversationId, LlmRole role, string content) =>
        new(AIEventTypes.MessageSent) { ConversationId = conversationId, Role = role, Content = content };

    public static MessageEvent Received(Guid conversationId, string content) =>
        new(AIEventTypes.MessageReceived) { ConversationId = conversationId, Role = LlmRole.Assistant, Content = content };
}

/// <summary>
/// Tool execution event.
/// </summary>
public sealed record ToolExecutedEvent : AIEvent
{
    public override string EventType => AIEventTypes.ToolExecuted;
    public required Guid ConversationId { get; init; }
    public required string ToolName { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public long ExecutionTimeMs { get; init; }
}

/// <summary>
/// RAG query event.
/// </summary>
public sealed record RagQueryEvent : AIEvent
{
    public override string EventType => AIEventTypes.RagQueryPerformed;
    public required Guid ConversationId { get; init; }
    public required string Query { get; init; }
    public int ResultCount { get; init; }
    public float TopScore { get; init; }
    public long QueryTimeMs { get; init; }
}

/// <summary>
/// Document indexed event.
/// </summary>
public sealed record DocumentIndexedEvent : AIEvent
{
    public override string EventType => AIEventTypes.DocumentIndexed;
    public required Guid DocumentId { get; init; }
    public required string Domain { get; init; }
    public int ChunkCount { get; init; }
    public long IndexTimeMs { get; init; }
}
