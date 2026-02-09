using System;

namespace RPlus.Kernel.Integration.Application.Events;

public sealed record IntegrationKeyCreatedEvent(
    Guid KeyId,
    Guid PartnerId,
    string Environment,
    string Prefix,
    DateTime CreatedAtUtc)
{
    public const string EventName = "integration.key.created.v1";
}

public sealed record IntegrationKeyRotatedEvent(
    Guid KeyId,
    Guid PartnerId,
    string Environment,
    DateTime RotatedAtUtc)
{
    public const string EventName = "integration.key.rotated.v1";
}

public sealed record IntegrationKeyRevokedEvent(
    Guid KeyId,
    Guid PartnerId,
    string Environment,
    DateTime RevokedAtUtc)
{
    public const string EventName = "integration.key.revoked.v1";
}

public sealed record IntegrationKeyRequestedEvent(
    Guid PartnerId,
    DateTime RequestedAtUtc)
{
    public const string EventName = "integration.key.requested.v1";
}
