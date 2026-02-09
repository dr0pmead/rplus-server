namespace RPlus.SDK.Access.Events;

#nullable enable
public static class AccessEventTopics
{
    public const string AccessDecisionMade = "access.decision.made";
    public const string PermissionRegistered = "kernel.access.permission.registered.v1";
    public const string PermissionActivated = "kernel.access.permission.activated.v1";
    public const string IntegrationPermissionGranted = "kernel.access.permission.integration.granted.v1";
    public const string IntegrationPermissionRevoked = "kernel.access.permission.integration.revoked.v1";
}
