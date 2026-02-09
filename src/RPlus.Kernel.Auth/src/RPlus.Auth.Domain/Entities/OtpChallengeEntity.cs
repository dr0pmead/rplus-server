// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Domain.Entities.OtpChallengeEntity
// Assembly: RPlus.Auth.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 30F033C3-72B9-4343-BF9A-347F69FE04BB
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Domain.dll

using System;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class OtpChallengeEntity
{
  public Guid Id { get; set; }

  public string PhoneHash { get; set; } = string.Empty;

  public Guid? UserId { get; set; }

  public string CodeHash { get; set; } = string.Empty;

  public string ChallengeType { get; set; } = "login";

  public DateTime CreatedAt { get; set; }

  public DateTime ExpiresAt { get; set; }

  public DateTime? VerifiedAt { get; set; }

  public int AttemptsLeft { get; set; } = 3;

  public bool IsBlocked { get; set; }

  public DateTime? BlockedAt { get; set; }

  public string IssuerIp { get; set; } = string.Empty;

  public string IssuerDeviceId { get; set; } = string.Empty;

  public string DeliveryChannel { get; set; } = "sms";

  public string DeliveryStatus { get; set; } = "pending";

  public DateTime? DeliveredAt { get; set; }

  public string? DeliveryError { get; set; }
}
