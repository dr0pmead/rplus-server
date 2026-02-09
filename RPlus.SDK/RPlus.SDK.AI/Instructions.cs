namespace RPlus.SDK.AI;

/// <summary>
/// Agent instruction for specific domain/task.
/// </summary>
public sealed record Instruction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Domain (e.g., "hr", "support", "sales").
    /// </summary>
    public required string Domain { get; init; }
    
    /// <summary>
    /// Unique name within domain.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// System prompt template (supports {{variables}}).
    /// </summary>
    public required string SystemPrompt { get; init; }
    
    /// <summary>
    /// Available tool names.
    /// </summary>
    public IReadOnlyList<string>? AvailableTools { get; init; }
    
    /// <summary>
    /// Default conversation state.
    /// </summary>
    public Dictionary<string, object>? DefaultState { get; init; }
    
    /// <summary>
    /// Custom parameters.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }
    
    /// <summary>
    /// Whether the instruction is active.
    /// </summary>
    public bool IsActive { get; init; } = true;
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Instruction repository.
/// </summary>
public interface IInstructionRepository
{
    Task<Instruction?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Instruction?> GetByNameAsync(string domain, string name, CancellationToken ct = default);
    Task<IReadOnlyList<Instruction>> ListAsync(string? domain = null, CancellationToken ct = default);
    Task SaveAsync(Instruction instruction, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Template engine for instruction prompts.
/// </summary>
public interface IInstructionTemplateEngine
{
    /// <summary>
    /// Render instruction with variables.
    /// </summary>
    string Render(Instruction instruction, Dictionary<string, object> variables);
}
