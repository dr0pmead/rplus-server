namespace RPlus.Documents.Domain.Entities;

public sealed class DocumentFolder
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? DepartmentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "Project";

    public bool IsSystem { get; set; }

    public bool IsImmutable { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
