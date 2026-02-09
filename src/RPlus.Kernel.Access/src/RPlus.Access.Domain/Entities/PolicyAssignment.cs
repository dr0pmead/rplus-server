// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.PolicyAssignment
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class PolicyAssignment
{
  public Guid Id { get; set; } = Guid.NewGuid();

  public Guid TenantId { get; set; }

  public string TargetType { get; set; } = string.Empty;

  public string TargetId { get; set; } = string.Empty;

  public string PermissionId { get; set; } = string.Empty;

  public string Effect { get; set; } = "ALLOW";

  public DateTime? ExpiresAt { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public Permission? Permission { get; set; }
}
