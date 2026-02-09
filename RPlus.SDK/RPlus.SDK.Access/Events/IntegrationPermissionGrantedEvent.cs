using System;

#nullable enable
namespace RPlus.SDK.Access.Events;

public sealed record IntegrationPermissionGrantedEvent(
    Guid ApiKeyId,
    string PermissionId,
    string Actor,
    DateTime OccurredAt);
