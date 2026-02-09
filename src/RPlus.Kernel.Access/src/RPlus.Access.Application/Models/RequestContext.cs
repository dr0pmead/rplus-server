// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Models.RequestContext
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System;

#nullable enable
namespace RPlus.Access.Application.Models;

public record RequestContext
{
  public string IpAddress { get; init; } = string.Empty;

  public string UserAgent { get; init; } = string.Empty;

  public DateTime Timestamp { get; init; }

  public string Channel { get; init; } = string.Empty;

  public RequestContext()
  {
  }
}
