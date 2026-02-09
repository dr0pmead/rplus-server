namespace RPlus.HR.Domain.Entities;

public sealed class MilitaryRecord
{
    public Guid UserId { get; set; }

    public bool IsLiable { get; set; }

    public string? Rank { get; set; }

    public string? VusCode { get; set; }

    public string? LocalOffice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

