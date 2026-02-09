namespace RPlus.SDK.AI;

/// <summary>
/// Tool executor for AI function calling.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Execute a tool call.
    /// </summary>
    Task<ToolResult> ExecuteAsync(
        LlmToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Get all available tools.
    /// </summary>
    IReadOnlyList<LlmTool> GetAvailableTools();

    /// <summary>
    /// Check if a tool exists.
    /// </summary>
    bool HasTool(string name);
}

/// <summary>
/// Tool execution result.
/// </summary>
public sealed record ToolResult(
    bool Success,
    string? Output,
    string? Error = null)
{
    public static ToolResult Ok(string output) => new(true, output);
    public static ToolResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Context for tool execution.
/// </summary>
public sealed record ToolExecutionContext
{
    public Guid ConversationId { get; init; }
    public Guid? UserId { get; init; }
    public string Domain { get; init; } = "general";
    public Dictionary<string, object> State { get; init; } = [];
}

/// <summary>
/// Base interface for tool implementations.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Tool name (must be unique).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema for parameters.
    /// </summary>
    string? ParametersSchema { get; }

    /// <summary>
    /// Execute the tool.
    /// </summary>
    Task<ToolResult> ExecuteAsync(
        string arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);
}
