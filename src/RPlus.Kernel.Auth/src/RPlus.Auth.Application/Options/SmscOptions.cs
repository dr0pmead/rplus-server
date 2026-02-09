// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.SmscOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public class SmscOptions
{
  public const string SectionName = "Smsc";

  public string Login { get; set; } = string.Empty;

  public string Password { get; set; } = string.Empty;

  public string Sender { get; set; } = string.Empty;
}
