using System.Collections.Generic;
using RPlus.SDK.Access.Enums;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record GetIntegrationPermissionsResponse(
    List<string> Permissions,
    bool Success,
    string? Error,
    IntegrationDecision Decision);
