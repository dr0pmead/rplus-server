// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Models.AuthenticationContext
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Application.Models;

public record AuthenticationContext
{
  public int Aal { get; init; }

  public List<string> Amr { get; init; } = new();

  public DateTime? AuthTime { get; init; }

  public string? DeviceId { get; init; }

  public AuthenticationContext()
  {
  }
}
