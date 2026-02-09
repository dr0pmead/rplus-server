// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Domain.Entities.AuditLogEntity
// Assembly: RPlus.Auth.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 30F033C3-72B9-4343-BF9A-347F69FE04BB
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Domain.dll

using System;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class AuditLogEntity
{
  public Guid Id { get; set; }

  public Guid? UserId { get; set; }

  public string? PhoneHash { get; set; }

  public string Action { get; set; } = string.Empty;

  public string Result { get; set; } = "success";

  public string? ErrorCode { get; set; }

  public string? ErrorMessage { get; set; }

  public DateTime CreatedAt { get; set; }

  public string? Ip { get; set; }

  public string? UserAgent { get; set; }

  public string? DeviceId { get; set; }

  public string? Location { get; set; }

  public string? MetadataJson { get; set; }

  public string RiskLevel { get; set; } = "low";

  public bool IsSuspicious { get; set; }
}
