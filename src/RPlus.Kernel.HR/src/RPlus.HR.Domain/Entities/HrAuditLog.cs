namespace RPlus.HR.Domain.Entities;

public sealed class HrAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string ActorType { get; set; } = "unknown";

    public Guid? ActorUserId { get; set; }

    public string? ActorService { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    /// <summary>JSON object with changed fields (redacted where required).</summary>
    public string ChangesJson { get; set; } = "{}";
}

