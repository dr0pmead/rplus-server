namespace RPlus.Organization.Api.Contracts;

public sealed record CreateUserAssignmentRequest(
    Guid UserId,
    Guid? PositionId,
    Guid? NodeId,
    string Role,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsPrimary,
    decimal FtePercent,
    string? PositionTitle);

public sealed record UpdateUserAssignmentRequest(
    string? Role,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool? IsPrimary,
    decimal? FtePercent,
    bool? IsDeleted);
