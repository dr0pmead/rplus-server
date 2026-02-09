namespace RPlus.HR.Domain.Entities;

public sealed class HrCustomFieldValue
{
    public Guid UserId { get; set; }

    public string FieldKey { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "null";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
