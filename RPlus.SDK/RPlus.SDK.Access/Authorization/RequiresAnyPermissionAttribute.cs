namespace RPlus.SDK.Access.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresAnyPermissionAttribute : Attribute
{
    public RequiresAnyPermissionAttribute(params string[] permissionIds)
    {
        PermissionIds = permissionIds ?? Array.Empty<string>();
    }

    public IReadOnlyCollection<string> PermissionIds { get; }
}
