using System.Text.Json;

namespace RPlus.SDK.AI;

#region LLM Provider Abstraction

/// <summary>
/// LLM provider abstraction supporting streaming and tool calling.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Provider name (e.g., "ollama", "openai", "claude").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the provider is currently available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Synchronous completion (blocks until full response).
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Streaming completion (yields tokens as they arrive).
    /// Preferred for any request > 500ms.
    /// </summary>
    IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Pre-warm the model (load weights into memory).
    /// </summary>
    Task WarmupAsync(CancellationToken ct = default);
}

#endregion

#region Request/Response Models

/// <summary>
/// LLM request with messages and optional tools.
/// </summary>
public sealed record LlmRequest
{
    public string? Model { get; init; }
    public required IReadOnlyList<LlmMessage> Messages { get; init; }
    public float Temperature { get; init; } = 0.7f;
    public int MaxTokens { get; init; } = 2048;
    public IReadOnlyList<LlmTool>? Tools { get; init; }
    public LlmResponseFormat? Format { get; init; }
}

/// <summary>
/// Single message in a conversation.
/// </summary>
public sealed record LlmMessage(
    LlmRole Role,
    string Content,
    string? Name = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolCallId = null)
{
    public static LlmMessage System(string content) => new(LlmRole.System, content);
    public static LlmMessage User(string content) => new(LlmRole.User, content);
    public static LlmMessage Assistant(string content) => new(LlmRole.Assistant, content);
    public static LlmMessage Tool(string content, string toolCallId) => 
        new(LlmRole.Tool, content, ToolCallId: toolCallId);
}

/// <summary>
/// Message role in conversation.
/// </summary>
public enum LlmRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// Full LLM response (non-streaming).
/// </summary>
public sealed record LlmResponse(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    TimeSpan? ProcessingTime = null);

/// <summary>
/// Streaming chunk from LLM.
/// </summary>
public sealed record LlmStreamChunk
{
    public LlmStreamChunkType Type { get; init; }
    public string? Content { get; init; }
    public LlmToolCall? ToolCall { get; init; }
    public bool IsDone { get; init; }

    public static LlmStreamChunk Token(string content) =>
        new() { Type = LlmStreamChunkType.Token, Content = content };

    public static LlmStreamChunk Thinking(string content) =>
        new() { Type = LlmStreamChunkType.Thinking, Content = content };

    public static LlmStreamChunk ToolUse(LlmToolCall toolCall) =>
        new() { Type = LlmStreamChunkType.ToolUse, ToolCall = toolCall };

    public static LlmStreamChunk Done() =>
        new() { Type = LlmStreamChunkType.Done, IsDone = true };
}

/// <summary>
/// Stream chunk types for SSE events.
/// </summary>
public enum LlmStreamChunkType
{
    Thinking,
    Token,
    ToolUse,
    Done
}

#endregion

#region Tools

/// <summary>
/// Tool definition for function calling.
/// </summary>
public sealed record LlmTool(
    string Name,
    string Description,
    JsonDocument? Parameters = null);

/// <summary>
/// Tool call request from LLM.
/// </summary>
public sealed record LlmToolCall(
    string Id,
    string Name,
    string Arguments);

/// <summary>
/// Response format hint.
/// </summary>
public sealed record LlmResponseFormat(
    string Type,
    JsonDocument? Schema = null);

#endregion
