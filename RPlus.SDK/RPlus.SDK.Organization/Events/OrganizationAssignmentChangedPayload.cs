namespace RPlus.SDK.Organization.Events;

public sealed record OrganizationAssignmentChangedPayload(
    Guid TenantId,
    Guid UserId,
    Guid PositionId,
    Guid DepartmentId,
    string Role,
    bool IsDeleted,
    DateTime ValidFrom,
    DateTime? ValidTo,
    DateTime OccurredAt);

