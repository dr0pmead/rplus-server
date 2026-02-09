// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Interfaces.IAccessContextBuilder
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using RPlus.SDK.Access.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Interfaces;

public interface IAccessContextBuilder
{
  Task<RPlus.SDK.Access.Models.AccessContext> BuildContextAsync(
    Guid userId,
    string? action,
    Guid? nodeId,
    Dictionary<string, object>? rawContext,
    CancellationToken ct = default (CancellationToken));
}
