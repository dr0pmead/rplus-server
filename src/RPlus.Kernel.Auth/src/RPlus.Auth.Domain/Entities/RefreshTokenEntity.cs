// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Domain.Entities.RefreshTokenEntity
// Assembly: RPlus.Auth.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 30F033C3-72B9-4343-BF9A-347F69FE04BB
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Domain.dll

using System;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class RefreshTokenEntity
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public Guid SessionId { get; set; }

  public Guid DeviceId { get; set; }

  public string TokenHash { get; set; } = string.Empty;

  public string TokenFamily { get; set; } = string.Empty;

  public string DeviceFingerprint { get; set; } = string.Empty;

  public DateTime IssuedAt { get; set; }

  public DateTime ExpiresAt { get; set; }

  public DateTime? UsedAt { get; set; }

  public DateTime? RevokedAt { get; set; }

  public bool IsRevoked => this.RevokedAt.HasValue;

  public Guid? ReplacedById { get; set; }

  public string? LastIp { get; set; }

  public string? LastUserAgent { get; set; }

  public string? DpopThumbprint { get; set; }

  public DeviceEntity Device { get; set; } = null!;
}
