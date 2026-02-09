namespace RPlus.SDK.Access.Queries;

#nullable enable
public sealed record CheckPermissionResponse(
    bool IsAllowed,
    string Reason,
    string Scope);
