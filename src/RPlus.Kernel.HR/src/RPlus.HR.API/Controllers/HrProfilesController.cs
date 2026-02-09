using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RPlus.HR.Api.Authorization;
using RPlus.HR.Api.Services;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Application.Validation;
using RPlus.HR.Domain.Entities;
using RPlus.SDK.Access.Authorization;
using RPlus.SDK.Users.Events;
using RPlus.SDK.Eventing.Abstractions;
using RPlusGrpc.Loyalty;

namespace RPlus.HR.Api.Controllers;

[ApiController]
[Route("api/hr/profiles")]
[Authorize]
public sealed class HrProfilesController : ControllerBase
{
    private readonly IHrDbContext _db;
    private readonly DocumentsGateway _documents;
    private readonly LoyaltyService.LoyaltyServiceClient _loyalty;
    private readonly IEventPublisher _events;
    private readonly ILogger<HrProfilesController> _logger;

    public HrProfilesController(
        IHrDbContext db,
        DocumentsGateway documents,
        LoyaltyService.LoyaltyServiceClient loyalty,
        IEventPublisher events,
        ILogger<HrProfilesController> logger)
    {
        _db = db;
        _documents = documents;
        _loyalty = loyalty;
        _events = events;
        _logger = logger;
    }

    [HttpGet("{userId:guid}")]
    [AllowSelf]
    [RequiresPermission("hr.profile.view")]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var profile = await _db.EmployeeProfiles
            .AsNoTracking()
            .Include(x => x.Documents)
            .Include(x => x.FamilyMembers)
            .ThenInclude(x => x.Documents)
            .Include(x => x.MilitaryRecord)
            .Include(x => x.BankDetails)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (profile == null)
            return NotFound(new { error = "not_found" });

        return Ok(Map(profile));
    }

    [HttpGet("{userId:guid}/basic")]
    [AllowSelf]
    [RequiresPermission("hr.profile.view")]
    public async Task<IActionResult> GetBasicProfile(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var profile = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.UserId,
                x.FirstName,
                x.LastName,
                x.MiddleName,
                x.PhotoFileId
            })
            .FirstOrDefaultAsync(ct);

        if (profile == null)
            return NotFound(new { error = "not_found" });

        var avatarUrl = profile.PhotoFileId.HasValue
            ? $"/api/hr/profiles/{userId:D}/files/{profile.PhotoFileId:D}"
            : null;

        return Ok(new HrBasicProfileResponse
        {
            UserId = profile.UserId,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            MiddleName = profile.MiddleName,
            PhotoFileId = profile.PhotoFileId,
            AvatarUrl = avatarUrl
        });
    }

    [HttpPut("{userId:guid}")]
    [AllowSelf]
    [RequiresPermission("hr.profile.edit")]
    public async Task<IActionResult> UpsertProfile(Guid userId, [FromBody] UpsertHrProfileRequest request, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var isSelf = TryGetCurrentUserId(out var currentUserId) && currentUserId == userId;
        if (isSelf && request.ContainsRestrictedFieldsForSelfEdit())
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden_fields" });

        var profile = await _db.EmployeeProfiles
            .Include(x => x.MilitaryRecord)
            .Include(x => x.BankDetails)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        var now = DateTime.UtcNow;
        if (profile == null)
        {
            profile = new EmployeeProfile
            {
                UserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.EmployeeProfiles.Add(profile);
        }

        if (request.PhotoFileId.HasValue)
        {
            var fileExists = await _db.HrFiles.AsNoTracking()
                .AnyAsync(x => x.Id == request.PhotoFileId.Value && x.OwnerUserId == userId, ct);
            if (!fileExists)
                return BadRequest(new { error = "invalid_photo_file" });
        }

        try
        {
            if (!isSelf)
            {
                ApplyAdminFields(profile, request);
            }

            ApplySelfFields(profile, request);
        }
        catch (HrValidationException vex)
        {
            return BadRequest(new { error = vex.Error });
        }

        if (profile.HireDate.HasValue)
        {
            var today = DateOnly.FromDateTime(now);
            if (profile.HireDate.Value > today)
                return BadRequest(new { error = "invalid_hire_date" });
        }

        if (profile.ShoeSize.HasValue && (profile.ShoeSize.Value < 20 || profile.ShoeSize.Value > 60))
        {
            return BadRequest(new { error = "invalid_shoe_size" });
        }

        profile.UpdatedAt = now;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict(new { error = "iin_conflict" });
        }

        if (profile.DocumentsFolderId == null)
        {
            var folderId = await _documents.EnsureUserFolderAsync(userId, ct);
            if (folderId.HasValue)
            {
                profile.DocumentsFolderId = folderId;
                profile.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        // Trigger loyalty level recalculation
        _ = Task.Run(async () =>
        {
            try
            {
                await _loyalty.RecalculateUserTenureAsync(new RecalculateUserTenureRequest { UserId = userId.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recalculate loyalty tenure for user {UserId}", userId);
            }
        });

        // Publish cache update event for scan profile
        var avatarUrl = profile.PhotoFileId.HasValue
            ? $"/api/hr/profiles/{userId:D}/files/{profile.PhotoFileId:D}"
            : null;

        await _events.PublishAsync(new HrProfileUpdatedEvent
        {
            UserId = userId,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            MiddleName = profile.MiddleName,
            AvatarUrl = avatarUrl,
            UpdatedAt = DateTimeOffset.UtcNow
        }, HrEventTopics.ProfileUpdated, userId.ToString(), ct).ConfigureAwait(false);

        return Ok(new { success = true });
    }

    private static void ApplyAdminFields(EmployeeProfile profile, UpsertHrProfileRequest request)
    {
        if (request.Iin != null)
        {
            var iin = NormalizeTrim(request.Iin);
            if (!KzIinValidator.IsValid(iin))
                throw new HrValidationException("invalid_iin");
            profile.Iin = iin;
        }

        if (request.FirstName != null) profile.FirstName = NormalizeName(request.FirstName, 128);
        if (request.LastName != null) profile.LastName = NormalizeName(request.LastName, 128);
        if (request.MiddleName != null) profile.MiddleName = NormalizeNullable(request.MiddleName, 128);
        if (request.Gender != null) profile.Gender = request.Gender;
        if (request.BirthDate.HasValue) profile.BirthDate = request.BirthDate;
        if (request.PlaceOfBirth != null) profile.PlaceOfBirth = NormalizeNullable(request.PlaceOfBirth, 256);
        if (request.Citizenship != null) profile.Citizenship = NormalizeCountryCode(request.Citizenship);
        if (request.HireDate.HasValue) profile.HireDate = request.HireDate;
        if (request.Status != null) profile.Status = request.Status.Value;
        if (request.BloodType != null) profile.BloodType = NormalizeNullable(request.BloodType, 16);
        if (request.DisabilityGroup != null) profile.DisabilityGroup = request.DisabilityGroup.Value;

        if (request.BankDetails != null)
        {
            profile.BankDetails ??= new BankDetails { UserId = profile.UserId, CreatedAt = DateTime.UtcNow };
            profile.BankDetails.Iban = NormalizeNullable(request.BankDetails.Iban, 64);
            profile.BankDetails.BankName = NormalizeNullable(request.BankDetails.BankName, 128);
            profile.BankDetails.UpdatedAt = DateTime.UtcNow;
        }

        if (request.MilitaryRecord != null)
        {
            profile.MilitaryRecord ??= new MilitaryRecord { UserId = profile.UserId, CreatedAt = DateTime.UtcNow };
            profile.MilitaryRecord.IsLiable = request.MilitaryRecord.IsLiable;
            profile.MilitaryRecord.Rank = NormalizeNullable(request.MilitaryRecord.Rank, 64);
            profile.MilitaryRecord.VusCode = NormalizeNullable(request.MilitaryRecord.VusCode, 64);
            profile.MilitaryRecord.LocalOffice = NormalizeNullable(request.MilitaryRecord.LocalOffice, 256);
            profile.MilitaryRecord.UpdatedAt = DateTime.UtcNow;
        }


        if (request.FamilyMembers != null)
        {
            profile.FamilyMembers.Clear();
            foreach (var f in request.FamilyMembers)
            {
                var member = new FamilyMember
                {
                    Id = f.Id == Guid.Empty ? Guid.NewGuid() : f.Id,
                    UserId = profile.UserId,
                    Relation = Enum.TryParse<FamilyRelation>(f.Relation, true, out var r) ? r : FamilyRelation.Other,
                    FullName = NormalizeName(f.FullName, 256),
                    BirthDate = f.BirthDate,
                    IsDependent = f.IsDependent,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                // Document mapping inside family member? 
                // Currently generic DTO implies flat docs? No, FamilyMemberDto has nested Documents.
                if (f.Documents != null)
                {
                    foreach (var d in f.Documents)
                    {
                         member.Documents.Add(new FamilyMemberDocument
                         {
                             Id = d.Id == Guid.Empty ? Guid.NewGuid() : d.Id,
                             Title = NormalizeName(d.Title, 128),
                             FileId = d.FileId,
                             FileName = NormalizeName(d.FileName, 256),
                             ContentType = d.ContentType,
                             Size = d.Size,
                             CreatedAt = DateTime.UtcNow
                         });
                    }
                }
                profile.FamilyMembers.Add(member);
            }
        }

        if (request.Documents != null)
        {
            profile.Documents.Clear();
            foreach (var d in request.Documents)
            {
                profile.Documents.Add(new EmployeeDocument
                {
                    Id = d.Id == Guid.Empty ? Guid.NewGuid() : d.Id,
                    UserId = profile.UserId,
                    Type = Enum.TryParse<EmployeeDocumentType>(d.Type, true, out var t) ? t : EmployeeDocumentType.Other,
                    Series = NormalizeNullable(d.Series, 32),
                    Number = NormalizeName(d.Number, 64),
                    IssueDate = d.IssueDate,
                    ExpiryDate = d.ExpiryDate,
                    IssuedBy = NormalizeNullable(d.IssuedBy, 256),
                    ScanFileId = d.ScanFileId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
    }

    private static void ApplySelfFields(EmployeeProfile profile, UpsertHrProfileRequest request)
    {
        if (request.PersonalEmail != null) profile.PersonalEmail = NormalizeNullableEmail(request.PersonalEmail);
        if (request.PersonalPhone != null) profile.PersonalPhone = NormalizeNullable(request.PersonalPhone, 32);
        if (request.ClothingSize != null) profile.ClothingSize = NormalizeNullable(request.ClothingSize, 16);
        if (request.ShoeSize != null) profile.ShoeSize = request.ShoeSize;
        if (request.PhotoFileId.HasValue) profile.PhotoFileId = request.PhotoFileId;

        if (request.RegistrationAddress != null) profile.RegistrationAddress = Map(request.RegistrationAddress);
        if (request.ResidentialAddress != null) profile.ResidentialAddress = Map(request.ResidentialAddress);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var sub = User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out userId) && userId != Guid.Empty;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    private static string NormalizeTrim(string value) => value.Trim();

    private static string NormalizeName(string value, int maxLen)
    {
        var s = value.Trim();
        if (s.Length == 0 || s.Length > maxLen)
            throw new HrValidationException("invalid_name");
        return s;
    }

    private static string? NormalizeNullable(string? value, int maxLen)
    {
        if (value == null)
            return null;

        var s = value.Trim();
        if (s.Length == 0)
            return null;
        if (s.Length > maxLen)
            throw new HrValidationException("invalid_value");
        return s;
    }

    private static string? NormalizeNullableEmail(string? email)
    {
        var s = NormalizeNullable(email, 320);
        if (s == null)
            return null;
        // basic safety checks; full email validation can be added later
        if (!s.Contains('@', StringComparison.Ordinal) || s.StartsWith('@') || s.EndsWith('@'))
            throw new HrValidationException("invalid_email");
        return s;
    }

    private static string NormalizeCountryCode(string value)
    {
        var s = value.Trim().ToUpperInvariant();
        if (s.Length != 2)
            throw new HrValidationException("invalid_citizenship");
        return s;
    }

    private static EmployeeAddress Map(EmployeeAddressDto dto) => new()
    {
        Region = NormalizeNullable(dto.Region, 128),
        District = NormalizeNullable(dto.District, 128),
        City = NormalizeNullable(dto.City, 128),
        Street = NormalizeNullable(dto.Street, 256),
        Building = NormalizeNullable(dto.Building, 64),
        Flat = NormalizeNullable(dto.Flat, 32)
    };

    private static HrProfileResponse Map(EmployeeProfile profile) => new()
    {
        UserId = profile.UserId,
        Iin = profile.Iin,
        FirstName = profile.FirstName,
        LastName = profile.LastName,
        MiddleName = profile.MiddleName,
        Gender = profile.Gender?.ToString(),
        BirthDate = profile.BirthDate,
        PlaceOfBirth = profile.PlaceOfBirth,
        Citizenship = profile.Citizenship,
        HireDate = profile.HireDate,
        Status = profile.Status.ToString(),
        BloodType = profile.BloodType,
        DisabilityGroup = profile.DisabilityGroup.ToString(),
        ClothingSize = profile.ClothingSize,
        ShoeSize = profile.ShoeSize,
        PersonalEmail = profile.PersonalEmail,
        PersonalPhone = profile.PersonalPhone,
        PhotoFileId = profile.PhotoFileId,
        DocumentsFolderId = profile.DocumentsFolderId,
        RegistrationAddress = profile.RegistrationAddress == null ? null : new EmployeeAddressDto(
            profile.RegistrationAddress.Region,
            profile.RegistrationAddress.District,
            profile.RegistrationAddress.City,
            profile.RegistrationAddress.Street,
            profile.RegistrationAddress.Building,
            profile.RegistrationAddress.Flat),
        ResidentialAddress = profile.ResidentialAddress == null ? null : new EmployeeAddressDto(
            profile.ResidentialAddress.Region,
            profile.ResidentialAddress.District,
            profile.ResidentialAddress.City,
            profile.ResidentialAddress.Street,
            profile.ResidentialAddress.Building,
            profile.ResidentialAddress.Flat),
        BankDetails = profile.BankDetails == null ? null : new BankDetailsDto(profile.BankDetails.Iban, profile.BankDetails.BankName),
        MilitaryRecord = profile.MilitaryRecord == null ? null : new MilitaryRecordDto(
            profile.MilitaryRecord.IsLiable,
            profile.MilitaryRecord.Rank,
            profile.MilitaryRecord.VusCode,
            profile.MilitaryRecord.LocalOffice),
        Documents = profile.Documents
            .OrderBy(x => x.CreatedAt)
            .Select(d => new EmployeeDocumentDto(
                d.Id,
                d.Type.ToString(),
                d.Series,
                d.Number,
                d.IssueDate,
                d.ExpiryDate,
                d.IssuedBy,
                d.ScanFileId))
            .ToArray(),
        FamilyMembers = profile.FamilyMembers
            .OrderBy(x => x.CreatedAt)
            .Select(f => new FamilyMemberDto(
                f.Id,
                f.Relation.ToString(),
                f.FullName,
                f.BirthDate,
                f.IsDependent,
                f.Documents
                    .OrderBy(d => d.CreatedAt)
                    .Select(d => new FamilyMemberDocumentDto(
                        d.Id,
                        d.Title,
                        d.FileId,
                        d.FileName,
                        d.ContentType,
                        d.Size,
                        d.CreatedAt))
                    .ToArray()))
            .ToArray(),
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt
    };

    public sealed record EmployeeAddressDto(
        string? Region,
        string? District,
        string? City,
        string? Street,
        string? Building,
        string? Flat);

    public sealed record BankDetailsDto(string? Iban, string? BankName);

    public sealed record MilitaryRecordDto(bool IsLiable, string? Rank, string? VusCode, string? LocalOffice);

    public sealed record EmployeeDocumentDto(
        Guid Id,
        string Type,
        string? Series,
        string Number,
        DateOnly? IssueDate,
        DateOnly? ExpiryDate,
        string? IssuedBy,
        Guid? ScanFileId);

    public sealed record FamilyMemberDto(
        Guid Id,
        string Relation,
        string FullName,
        DateOnly? BirthDate,
        bool IsDependent,
        FamilyMemberDocumentDto[] Documents);

    public sealed record FamilyMemberDocumentDto(
        Guid Id,
        string Title,
        Guid FileId,
        string FileName,
        string ContentType,
        long Size,
        DateTime CreatedAt);

    public sealed record HrProfileResponse
    {
        public Guid UserId { get; init; }
        public string? Iin { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string? MiddleName { get; init; }
        public string? Gender { get; init; }
        public DateOnly? BirthDate { get; init; }
        public string? PlaceOfBirth { get; init; }
        public string Citizenship { get; init; } = "KZ";
        public DateOnly? HireDate { get; init; }
        public string Status { get; init; } = EmployeeStatus.Active.ToString();
        public string? BloodType { get; init; }
        public string DisabilityGroup { get; init; } = RPlus.HR.Domain.Entities.DisabilityGroup.None.ToString();
        public string? ClothingSize { get; init; }
        public int? ShoeSize { get; init; }
        public string? PersonalEmail { get; init; }
        public string? PersonalPhone { get; init; }
        public Guid? PhotoFileId { get; init; }
        public Guid? DocumentsFolderId { get; init; }
        public EmployeeAddressDto? RegistrationAddress { get; init; }
        public EmployeeAddressDto? ResidentialAddress { get; init; }
        public BankDetailsDto? BankDetails { get; init; }
        public MilitaryRecordDto? MilitaryRecord { get; init; }
        public EmployeeDocumentDto[] Documents { get; init; } = Array.Empty<EmployeeDocumentDto>();
        public FamilyMemberDto[] FamilyMembers { get; init; } = Array.Empty<FamilyMemberDto>();
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public sealed record HrBasicProfileResponse
    {
        public Guid UserId { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string? MiddleName { get; init; }
        public Guid? PhotoFileId { get; init; }
        public string? AvatarUrl { get; init; }
    }

    public sealed record UpsertHrProfileRequest
    {
        public string? Iin { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? MiddleName { get; init; }
        public EmployeeGender? Gender { get; init; }
        public DateOnly? BirthDate { get; init; }
        public string? PlaceOfBirth { get; init; }
        public string? Citizenship { get; init; }
        public DateOnly? HireDate { get; init; }
        public EmployeeStatus? Status { get; init; }
        public string? BloodType { get; init; }
        public DisabilityGroup? DisabilityGroup { get; init; }
        public string? ClothingSize { get; init; }
        public int? ShoeSize { get; init; }
        public string? PersonalEmail { get; init; }
        public string? PersonalPhone { get; init; }
        public Guid? PhotoFileId { get; init; }
        public EmployeeAddressDto? RegistrationAddress { get; init; }
        public EmployeeAddressDto? ResidentialAddress { get; init; }
        public BankDetailsDto? BankDetails { get; init; }

        public MilitaryRecordDto? MilitaryRecord { get; init; }
        public FamilyMemberDto[]? FamilyMembers { get; init; }
        public EmployeeDocumentDto[]? Documents { get; init; }

        public bool ContainsRestrictedFieldsForSelfEdit() =>
            Iin != null
            || FirstName != null
            || LastName != null
            || MiddleName != null
            || Gender != null
            || BirthDate != null
            || PlaceOfBirth != null
            || Citizenship != null
            || HireDate != null
            || Status != null
            || BloodType != null
            || DisabilityGroup != null
            || BankDetails != null
            || BankDetails != null
            || MilitaryRecord != null
            || FamilyMembers != null
            || Documents != null;
    }

    private sealed class HrValidationException : Exception
    {
        public HrValidationException(string error) : base(error)
        {
            Error = error;
        }

        public string Error { get; }
    }
}
