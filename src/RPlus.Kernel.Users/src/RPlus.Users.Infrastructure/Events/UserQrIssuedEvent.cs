using System;

namespace RPlus.Users.Infrastructure.Events;

public sealed record UserQrIssuedEvent(
    Guid UserId,
    string Token,
    DateTimeOffset ExpiresAt)
{
    public const string EventName = "users.qr.issued.v1";
}
