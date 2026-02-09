// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.MobizonOtpSender
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Options;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class MobizonOtpSender : IOtpDeliveryService
{
  private readonly ILogger<MobizonOtpSender> _logger;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOptionsMonitor<MobizonOptions> _options;

  public MobizonOtpSender(
    ILogger<MobizonOtpSender> logger,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MobizonOptions> options)
  {
    this._logger = logger;
    this._httpClientFactory = httpClientFactory;
    this._options = options;
  }

  public async Task<bool> DeliverAsync(
    string phone,
    string code,
    string? channel,
    CancellationToken cancellationToken)
  {
    MobizonOptions currentValue = this._options.CurrentValue;
    return await this.SendSmsAsync(phone, code, currentValue, cancellationToken);
  }

  private async Task<bool> SendSmsAsync(
    string phone,
    string code,
    MobizonOptions config,
    CancellationToken cancellationToken)
  {
    try
    {
      string stringToEscape = "НИКОМУ НЕ СООБЩАЙТЕ ДАННЫЙ КОД! Ваш код: " + code;
      if (config.EnableMock)
      {
        this._logger.LogInformation("[MOCK] SMS accepted for {PhoneMasked}", (object) MaskPhone(phone));
        return true;
      }
      using (HttpClient client = this._httpClientFactory.CreateClient())
      {
        string requestUri = $"{config.ApiUrl}?apiKey={config.ApiKey}&recipient={phone.TrimStart('+')}&text={Uri.EscapeDataString(stringToEscape)}";
        if (!string.IsNullOrEmpty(config.SenderName))
          requestUri = $"{requestUri}&from={config.SenderName}";
        HttpResponseMessage response = await client.GetAsync(requestUri, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        this._logger.LogInformation("Mobizon response status {StatusCode} (Phone={PhoneMasked})", (object) response.StatusCode, (object) MaskPhone(phone));
        if (!response.IsSuccessStatusCode)
          return false;
        MobizonOtpSender.MobizonResponse mobizonResponse = JsonSerializer.Deserialize<MobizonOtpSender.MobizonResponse>(json);
        return mobizonResponse != null && mobizonResponse.Code == 0;
      }
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Mobizon SMS send failed (Phone={PhoneMasked})", (object) MaskPhone(phone));
      return false;
    }
  }

  private static string MaskPhone(string phone)
  {
    if (string.IsNullOrWhiteSpace(phone))
      return "<empty>";
    string trimmed = phone.Trim();
    int keep = Math.Min(2, trimmed.Length);
    return new string('*', Math.Max(0, trimmed.Length - keep)) + trimmed.Substring(trimmed.Length - keep, keep);
  }

  private class MobizonResponse
  {
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
  }
}
