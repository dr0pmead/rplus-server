namespace RPlus.HR.Domain.Entities;

public sealed class FamilyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public FamilyRelation Relation { get; set; } = FamilyRelation.Child;

    public string FullName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    public bool IsDependent { get; set; }

    public List<FamilyMemberDocument> Documents { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
