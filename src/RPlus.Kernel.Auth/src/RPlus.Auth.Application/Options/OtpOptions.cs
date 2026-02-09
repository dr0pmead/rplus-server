// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.OtpOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public sealed class OtpOptions
{
  public const string SectionName = "Otp";

  public int Digits { get; init; } = 6;

  public int TtlSeconds { get; init; } = 300;

  public int MaxAttempts { get; init; } = 3;

  // SECURITY: never expose OTP codes to clients in production.
  // Enable only for local/dev environments (e.g. docker-compose.frontend.yml).
  public bool ExposeDebugCode { get; init; } = false;
}
