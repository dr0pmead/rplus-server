using System;

namespace RPlus.Kernel.Integration.Application.Events;

public sealed record IntegrationPartnerUpdatedEvent(
    Guid PartnerId,
    string Name,
    string AccessLevel,
    bool IsDiscountPartner,
    bool IsActive,
    DateTime UpdatedAtUtc)
{
    public const string EventName = "integration.partner.updated.v1";
}

public sealed record IntegrationPartnerDeletedEvent(
    Guid PartnerId,
    DateTime DeletedAtUtc)
{
    public const string EventName = "integration.partner.deleted.v1";
}
