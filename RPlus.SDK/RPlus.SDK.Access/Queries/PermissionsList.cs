using System.Collections.Generic;
using RPlus.SDK.Access.DTOs;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record PermissionsList(List<PermissionDto> Permissions);
