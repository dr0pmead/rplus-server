// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Options.JwtOptions
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

#nullable enable
namespace RPlus.Core.Options;

public class JwtOptions
{
  public const string SectionName = "JWT";

  public string SigningKey { get; set; } = string.Empty;

  public string PublicKeyPem { get; set; } = string.Empty;

  public string Authority { get; set; } = string.Empty;

  public string Issuer { get; set; } = string.Empty;

  public string Audience { get; set; } = string.Empty;

  public int ExpiryMinutes { get; set; } = 60;
}
