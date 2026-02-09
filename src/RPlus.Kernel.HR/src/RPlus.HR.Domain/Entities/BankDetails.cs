namespace RPlus.HR.Domain.Entities;

public sealed class BankDetails
{
    public Guid UserId { get; set; }

    public string? Iban { get; set; }

    public string? BankName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

