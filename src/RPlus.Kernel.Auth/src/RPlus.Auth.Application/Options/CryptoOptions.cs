// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.CryptoOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public sealed class CryptoOptions
{
  public const string SectionName = "Crypto";

  public string PhoneHashingSalt { get; init; } = string.Empty;

  public string OtpSigningKey { get; init; } = string.Empty;

  public string? Salt { get; init; }

  public int Iterations { get; init; } = 50000;
}
