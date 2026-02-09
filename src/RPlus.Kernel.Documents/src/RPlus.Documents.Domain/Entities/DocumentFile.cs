namespace RPlus.Documents.Domain.Entities;

public sealed class DocumentFile
{
    public Guid Id { get; set; }

    public Guid FolderId { get; set; }

    public Guid? OwnerUserId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? DepartmentId { get; set; }

    public string? SubjectType { get; set; }

    public Guid? SubjectId { get; set; }

    public string? DocumentType { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }
}
