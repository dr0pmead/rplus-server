using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.DTOs;

public sealed record PermissionDto(
    string PermissionId,
    Guid ApplicationId,
    List<string>? SupportedContexts,
    bool IsActive);
