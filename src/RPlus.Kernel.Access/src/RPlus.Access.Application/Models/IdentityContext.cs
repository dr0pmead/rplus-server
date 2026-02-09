// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Models.IdentityContext
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Application.Models;

public record IdentityContext
{
  public Guid UserId { get; init; }

  public Guid TenantId { get; init; }

  public HashSet<string> EffectiveRoles { get; init; } = new();

  public Guid? ActingUserId { get; init; }

  public Guid? DelegationId { get; init; }

  public IdentityContext()
  {
  }
}
