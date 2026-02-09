using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.Events;

public sealed record PermissionRegisteredEvent(
    string PermissionId,
    Guid ApplicationId,
    List<string>? SupportedContexts,
    string Actor,
    DateTime OccurredAt = default);
