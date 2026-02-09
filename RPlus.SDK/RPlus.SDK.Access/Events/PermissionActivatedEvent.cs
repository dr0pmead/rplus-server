using System;

#nullable enable
namespace RPlus.SDK.Access.Events;

public sealed record PermissionActivatedEvent(
    string PermissionId,
    Guid ApplicationId,
    string Actor,
    DateTime OccurredAt);
