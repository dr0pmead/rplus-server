namespace RPlus.HR.Domain.Entities;

public sealed class HrCustomFieldDefinition
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = "text";

    public bool Required { get; set; }

    public string Group { get; set; } = "General";

    public int Order { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystem { get; set; }

    public int? MinLength { get; set; }

    public int? MaxLength { get; set; }

    public string? Pattern { get; set; }

    public string? Placeholder { get; set; }

    /// <summary>JSON serialized options (for select, multi-select, etc).</summary>
    public string? OptionsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
