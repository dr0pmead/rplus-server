using System;
using RPlus.SDK.Access.Models;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class Permission : RPlus.SDK.Access.Models.Permission
{
    public Guid AppId { get; set; }
    public App? App { get; set; }
    public string[] SupportedContexts { get; set; } = Array.Empty<string>();

    // Service that last published this permission (for auto-deprecation on manifest updates).
    public string? SourceService { get; set; }
}
