using System;

namespace RPlus.SDK.Access.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresPermissionAttribute : Attribute
{
    public RequiresPermissionAttribute(string permissionId)
    {
        PermissionId = permissionId ?? throw new ArgumentNullException(nameof(permissionId));
    }

    public string PermissionId { get; }
}

