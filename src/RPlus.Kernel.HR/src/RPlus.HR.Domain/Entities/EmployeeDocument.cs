namespace RPlus.HR.Domain.Entities;

public sealed class EmployeeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public EmployeeDocumentType Type { get; set; } = EmployeeDocumentType.IdentityCard_KZ;

    public string? Series { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? IssuedBy { get; set; }

    public Guid? ScanFileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

