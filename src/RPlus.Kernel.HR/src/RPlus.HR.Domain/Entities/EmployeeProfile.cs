using RPlus.HR.Domain.Entities;

public sealed class EmployeeProfile
{
    public Guid UserId { get; set; }

    /// <summary>Kazakhstan IIN (12 digits). Unique.</summary>
    public string? Iin { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public EmployeeGender? Gender { get; set; }

    public DateOnly? HireDate { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? PlaceOfBirth { get; set; }

    /// <summary>ISO 3166-1 alpha-2. Default: KZ.</summary>
    public string Citizenship { get; set; } = "KZ";

    public string? BloodType { get; set; }

    public DisabilityGroup DisabilityGroup { get; set; } = DisabilityGroup.None;

    public string? ClothingSize { get; set; }

    public int? ShoeSize { get; set; }

    public string? PersonalEmail { get; set; }

    public string? PersonalPhone { get; set; }

    public Guid? PhotoFileId { get; set; }

    public Guid? DocumentsFolderId { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public EmployeeAddress? RegistrationAddress { get; set; }

    public EmployeeAddress? ResidentialAddress { get; set; }

    public List<EmployeeDocument> Documents { get; set; } = new();

    public List<FamilyMember> FamilyMembers { get; set; } = new();

    public MilitaryRecord? MilitaryRecord { get; set; }

    public BankDetails? BankDetails { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>JSONB storage for custom fields</summary>
    public string? CustomDataJson { get; set; }
}
