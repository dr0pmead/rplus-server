// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Domain.Entities.AuthKnownUserEntity
// Assembly: RPlus.Auth.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 30F033C3-72B9-4343-BF9A-347F69FE04BB
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Domain.dll

using System;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class AuthKnownUserEntity
{
  public Guid UserId { get; set; }

  public string PhoneHash { get; set; } = string.Empty;

  public bool IsActive { get; set; } = true;

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }
}
