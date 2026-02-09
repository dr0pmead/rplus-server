// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.SmsSendResult
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public sealed record SmsSendResult(bool Success, string? MessageId, string? Error)
{
  public static SmsSendResult Successful(string? messageId)
  {
    return new SmsSendResult(true, messageId, (string) null);
  }

  public static SmsSendResult Failed(string? error)
  {
    return new SmsSendResult(false, (string) null, string.IsNullOrWhiteSpace(error) ? "unknown_error" : error);
  }
}
