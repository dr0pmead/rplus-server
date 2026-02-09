// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.AccessPolicy
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class AccessPolicy
{
  public Guid Id { get; set; }

  public Guid TenantId { get; set; }

  public Guid RoleId { get; set; }

  public string PermissionId { get; set; } = string.Empty;

  public string Effect { get; set; } = "ALLOW";

  public string ScopeType { get; set; } = "GLOBAL";

  public string? ConditionExpression { get; set; }

  public int Priority { get; set; }

  public int? RequiredAuthLevel { get; set; }

  public int? MaxAuthAgeSeconds { get; set; }

  public Role? Role { get; set; }

  public Permission? Permission { get; set; }

  public DateTime CreatedAt { get; set; }

  [Timestamp]
  public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
