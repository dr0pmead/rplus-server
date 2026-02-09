// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.LocalUserAssignment
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class LocalUserAssignment
{
  public Guid TenantId { get; set; }

  public Guid UserId { get; set; }

  public Guid NodeId { get; set; }

  public string RoleCode { get; set; } = string.Empty;

  public string PathSnapshot { get; set; } = string.Empty;
}
