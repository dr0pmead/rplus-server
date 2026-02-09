// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.EffectiveSnapshot
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class EffectiveSnapshot
{
  public Guid Id { get; set; } = Guid.NewGuid();

  public Guid UserId { get; set; }

  public Guid TenantId { get; set; }

  public string Context { get; set; } = "global";

  public string DataJson { get; set; } = "{}";

  public long Version { get; set; }

  public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

  public DateTime ExpiresAt { get; set; }
}
