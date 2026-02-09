namespace RPlus.SDK.Access.Queries;

#nullable enable
public sealed record GetEffectiveRightsResponse(
    string PermissionsJson,
    long Version);
