using System;

#nullable enable
namespace RPlus.SDK.Access.Events;

public sealed record IntegrationPermissionRevokedEvent(
    Guid ApiKeyId,
    string PermissionId,
    string Actor,
    DateTime OccurredAt);
