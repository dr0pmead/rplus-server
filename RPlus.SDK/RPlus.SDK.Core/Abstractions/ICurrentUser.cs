// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.ICurrentUser
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public interface ICurrentUser
{
  string? Id { get; }

  string? TenantId { get; }

  string? Email { get; }

  bool IsAuthenticated { get; }

  IEnumerable<string> Roles { get; }

  bool HasPermission(string permissionCode);
}
