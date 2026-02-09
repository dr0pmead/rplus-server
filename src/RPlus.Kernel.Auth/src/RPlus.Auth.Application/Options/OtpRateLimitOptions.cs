// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.OtpRateLimitOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public sealed class OtpRateLimitOptions
{
  public const string SectionName = "OtpRateLimit";

  public int PerMinute { get; init; } = 3;

  public int WindowSeconds { get; init; } = 60;

  public (int limit, int windowSeconds) GetPerMinuteLimit() => (this.PerMinute, this.WindowSeconds);
}
