// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.SmsOtpSender
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Options;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class SmsOtpSender : IOtpDeliveryService
{
  private readonly ILogger<SmsOtpSender> _logger;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOptionsMonitor<SmsOptions> _smsOptions;

  public SmsOtpSender(
    ILogger<SmsOtpSender> logger,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SmsOptions> smsOptions)
  {
    this._logger = logger;
    this._httpClientFactory = httpClientFactory;
    this._smsOptions = smsOptions;
  }

  public async Task<bool> DeliverAsync(
    string phone,
    string code,
    string? channel,
    CancellationToken cancellationToken)
  {
    SmsOptions currentValue = this._smsOptions.CurrentValue;
    string stringToEscape = $"[R+] Ваш код подтверждения: {code}. Не сообщайте его никому.";
    if (currentValue.Simulate)
    {
      this._logger.LogInformation("SMS simulation: message accepted for {PhoneMasked}", (object) MaskPhone(phone));
      return true;
    }
    try
    {
      using (HttpClient client = this._httpClientFactory.CreateClient())
      {
        HttpResponseMessage async = await client.GetAsync($"{currentValue.ApiUrl}?api_id={currentValue.ApiKey}&to={phone}&msg={Uri.EscapeDataString(stringToEscape)}&json=1", cancellationToken);
        if (async.IsSuccessStatusCode)
        {
          this._logger.LogInformation("SMS successfully sent to {PhoneMasked}", (object) MaskPhone(phone));
          return true;
        }
        this._logger.LogError("SMS provider returned error: {Status} (Phone={PhoneMasked})", (object) async.StatusCode, (object) MaskPhone(phone));
        return false;
      }
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to send SMS (Phone={PhoneMasked})", (object) MaskPhone(phone));
      return false;
    }
  }

  private static string MaskPhone(string phone)
  {
    if (string.IsNullOrWhiteSpace(phone))
      return "<empty>";
    string digitsOnly = phone.Trim();
    int keep = Math.Min(2, digitsOnly.Length);
    return new string('*', Math.Max(0, digitsOnly.Length - keep)) + digitsOnly.Substring(digitsOnly.Length - keep, keep);
  }
}
