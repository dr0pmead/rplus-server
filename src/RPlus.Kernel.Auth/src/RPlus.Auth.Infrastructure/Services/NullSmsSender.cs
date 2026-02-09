// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Services.NullSmsSender
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Services;

public sealed class NullSmsSender : ISmsSender
{
  private readonly ILogger<NullSmsSender> _logger;

  public NullSmsSender(ILogger<NullSmsSender> logger) => this._logger = logger;

  public Task<SmsSendResult> SendAsync(
    string phoneE164,
    string message,
    CancellationToken cancellationToken)
  {
    this._logger.LogInformation("Skipping SMS send (null sender) for {PhoneMasked}", (object) MaskPhone(phoneE164));
    return Task.FromResult<SmsSendResult>(SmsSendResult.Successful((string) null));
  }

  private static string MaskPhone(string phone)
  {
    if (string.IsNullOrWhiteSpace(phone))
      return "<empty>";
    string trimmed = phone.Trim();
    int keep = Math.Min(2, trimmed.Length);
    return new string('*', Math.Max(0, trimmed.Length - keep)) + trimmed.Substring(trimmed.Length - keep, keep);
  }
}
