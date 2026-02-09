namespace RPlus.Kernel.Integration.Application.Events;

public sealed record IntegrationPartnerCreatedEvent(
    Guid PartnerId,
    string Name,
    string AccessLevel,
    bool IsDiscountPartner,
    DateTime CreatedAtUtc)
{
    public const string EventName = "integration.partner.created.v1";
}
