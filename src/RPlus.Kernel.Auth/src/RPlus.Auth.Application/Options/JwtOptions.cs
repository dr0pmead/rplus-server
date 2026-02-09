// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.JwtOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using System;

#nullable enable
namespace RPlus.Auth.Options;

public sealed class JwtOptions
{
  public const string SectionName = "Jwt";

  public string? Issuer { get; init; }

  public string? Audience { get; init; }

  public string? SigningKey { get; init; }

  public string? PrivateKeyPem { get; init; }

  public string? PublicKeyPem { get; init; }

  public int RotateEveryHours { get; init; } = 24;

  public int RetainForHours { get; init; } = 168;

  public int KeySize { get; init; } = 3072 /*0x0C00*/;

  public int KeyCacheSeconds { get; init; } = 30;

  public int AccessTokenMinutes { get; init; } = 60;

  public int RefreshTokenMinutes { get; init; } = 43200;

  public int AccessMinutes => this.AccessTokenMinutes;

  public int RefreshDays => (int) Math.Ceiling((double) this.RefreshTokenMinutes / 1440.0);
}
