using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record ValidateApiKeyResponse(
    bool Success,
    List<string> Permissions,
    string? Error);
