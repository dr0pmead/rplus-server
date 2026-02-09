// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.IPermissionRegistry
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public interface IPermissionRegistry
{
  Task<bool> RegisterAsync(
    string permissionId,
    string? applicationId,
    string[] supportedContexts,
    CancellationToken ct);
}
