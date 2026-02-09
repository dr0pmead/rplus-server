using System;

namespace RPlus.SDK.Contracts.Events;

public record UserLoginFailed(
    string? UserId,
    string Identifier,
    string Reason,
    string IpAddress,
    string? UserAgent,
    DateTime OccurredAt
);
