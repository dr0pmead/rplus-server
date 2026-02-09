namespace RPlus.HR.Domain.Entities;

public sealed class FamilyMemberDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyMemberId { get; set; }

    public Guid UserId { get; set; }

    public Guid FileId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public FamilyMember? FamilyMember { get; set; }
}
