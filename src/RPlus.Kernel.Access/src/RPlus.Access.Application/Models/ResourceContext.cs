// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Models.ResourceContext
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System;

#nullable enable
namespace RPlus.Access.Application.Models;

public record ResourceContext
{
  public Guid? NodeId { get; init; }

  public string? ResourceId { get; init; }

  public string? ResourceType { get; init; }

  public Guid? OwnerId { get; init; }

  public ResourceContext()
  {
  }
}
