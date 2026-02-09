// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Models.AccessContext
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Application.Models;

public record AccessContext
{
  public IdentityContext Identity { get; init; } = new();

  public AuthenticationContext Authentication { get; init; } = new();

  public ResourceContext Resource { get; init; } = new();

  public RequestContext Request { get; init; } = new();

  public Dictionary<string, object> Attributes { get; init; } = new();

  public AccessContext()
  {
  }
}
