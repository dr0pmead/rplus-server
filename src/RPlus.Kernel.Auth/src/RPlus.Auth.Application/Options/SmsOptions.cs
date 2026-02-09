// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.SmsOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public sealed class SmsOptions
{
  public const string SectionName = "Sms";

  public bool Simulate { get; set; } = true;

  public string ApiKey { get; set; } = string.Empty;

  public string SenderName { get; set; } = "RPlus";

  public string ApiUrl { get; set; } = "https://sms.ru/sms/send";
}
