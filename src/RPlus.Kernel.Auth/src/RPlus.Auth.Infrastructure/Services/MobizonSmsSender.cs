// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Services.MobizonSmsSender
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Services;

public sealed class MobizonSmsSender : ISmsSender
{
  private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
  private readonly HttpClient _httpClient;
  private readonly IOptions<MobizonOptions> _options;
  private readonly ILogger<MobizonSmsSender> _logger;

  public MobizonSmsSender(
    HttpClient httpClient,
    IOptions<MobizonOptions> options,
    ILogger<MobizonSmsSender> logger)
  {
    this._httpClient = httpClient;
    this._options = options;
    this._logger = logger;
    this.ConfigureHttpClient(options.Value);
  }

  public async Task<SmsSendResult> SendAsync(
    string phoneE164,
    string message,
    CancellationToken cancellationToken)
  {
    MobizonOptions mobizonOptions = this._options.Value;
    if (string.IsNullOrWhiteSpace(mobizonOptions.ApiKey))
    {
      this._logger.LogWarning("Mobizon API key is not configured, SMS will not be sent.");
      return SmsSendResult.Failed("mobizon_api_key_missing");
    }
    string str1;
    if (!phoneE164.StartsWith("+", StringComparison.Ordinal))
    {
      str1 = phoneE164;
    }
    else
    {
      string str2 = phoneE164;
      str1 = str2.Substring(1, str2.Length - 1);
    }
    string recipient = str1;
    Dictionary<string, string> nameValueCollection = new Dictionary<string, string>()
    {
      ["apiKey"] = mobizonOptions.ApiKey,
      ["recipient"] = recipient,
      ["text"] = message
    };
    if (!string.IsNullOrWhiteSpace(mobizonOptions.SenderName))
    {
      nameValueCollection["from"] = mobizonOptions.SenderName;
      this._logger.LogDebug("Sending SMS with sender: {Sender}", (object) mobizonOptions.SenderName);
    }
    else
      this._logger.LogDebug("Sending SMS without sender (will use system default)");
    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, mobizonOptions.ApiUrl)
    {
      Content = (HttpContent) new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>) nameValueCollection)
    })
    {
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      this._logger.LogInformation("Sending SMS via Mobizon. Recipient={Recipient}, HasSender={HasSender}", (object) recipient, (object) !string.IsNullOrWhiteSpace(mobizonOptions.SenderName));
      try
      {
        using (HttpResponseMessage response1 = await this._httpClient.SendAsync(request, cancellationToken))
        {
          string raw = await response1.Content.ReadAsStringAsync(cancellationToken);
          if (!response1.IsSuccessStatusCode)
          {
            this._logger.LogWarning("Mobizon returned HTTP {Status}: {Body}", (object) response1.StatusCode, (object) raw);
            return SmsSendResult.Failed($"HTTP {(int) response1.StatusCode}");
          }
          SmsSendResult response2 = this.ParseResponse(raw);
          if (response2.Success)
            this._logger.LogInformation("SMS sent successfully via Mobizon. Recipient={Recipient}, MessageId={MessageId}", (object) recipient, (object) response2.MessageId);
          else
            this._logger.LogWarning("SMS send failed via Mobizon. Recipient={Recipient}, Error={Error}", (object) recipient, (object) response2.Error);
          return response2;
        }
      }
      catch (Exception ex)
      {
        this._logger.LogError(ex, "Error while sending SMS via Mobizon. Recipient={Recipient}", (object) recipient);
        return SmsSendResult.Failed("mobizon_send_error");
      }
    }
  }

  private void ConfigureHttpClient(MobizonOptions options)
  {
    this._httpClient.Timeout = TimeSpan.FromSeconds((long) Math.Clamp(options.TimeoutSeconds, 5, 120));
    this._httpClient.DefaultRequestHeaders.AcceptEncoding.Clear();
    this._httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
    this._httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
  }

  private SmsSendResult ParseResponse(string raw)
  {
    try
    {
      MobizonSmsSender.MobizonResponse response = JsonSerializer.Deserialize<MobizonSmsSender.MobizonResponse>(raw, MobizonSmsSender.JsonOptions);
      if (response == null)
      {
        this._logger.LogWarning("Mobizon response is empty: {Body}", (object) raw);
        return SmsSendResult.Failed("mobizon_empty_response");
      }
      if (response.Code == 0)
        return SmsSendResult.Successful(response.Data?["messageId"]?.GetValue<string>() ?? response.Data?["id"]?.GetValue<string>());
      if (response.Code == 100)
      {
        string messageId = response.Data?["taskId"]?.GetValue<string>();
        this._logger.LogInformation("SMS queued for background processing. TaskId={TaskId}", (object) messageId);
        return SmsSendResult.Successful(messageId);
      }
      string error = MobizonSmsSender.BuildErrorMessage(response);
      this._logger.LogWarning("Mobizon returned error {Code}: {Error}. Raw={Raw}", (object) response.Code, (object) error, (object) raw);
      return SmsSendResult.Failed(error);
    }
    catch (JsonException ex)
    {
      this._logger.LogWarning((Exception) ex, "Failed to parse Mobizon response: {Body}", (object) raw);
      return SmsSendResult.Failed("mobizon_response_parse_error");
    }
  }

  private static string BuildErrorMessage(MobizonSmsSender.MobizonResponse response)
  {
    string str1 = response.Message ?? "mobizon_error";
    JsonNode jsonNode;
    if (response.Data == null || !response.Data.TryGetPropertyValue("invalidParams", out jsonNode) || !(jsonNode is JsonObject source) || source.Count <= 0)
      return str1;
    string str2 = string.Join("; ", source.Select<KeyValuePair<string, JsonNode>, string>((Func<KeyValuePair<string, JsonNode>, string>) (kvp => $"{kvp.Key}: {kvp.Value?.GetValue<string>() ?? "invalid"}")));
    return $"{str1}. {str2}";
  }

  private sealed class MobizonResponse
  {
    public int Code { get; set; }

    public string? Message { get; set; }

    public JsonObject? Data { get; set; }
  }
}
