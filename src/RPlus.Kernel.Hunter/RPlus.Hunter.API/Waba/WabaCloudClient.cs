using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPlus.Hunter.API.Waba;

/// <summary>
/// Direct client for Meta Graph API v21.0.
/// 
/// Replaces 360dialog intermediary — zero middleman, zero monthly fees.
/// URL: https://graph.facebook.com/v21.0/{PhoneNumberId}/messages
/// Auth: Authorization: Bearer {AccessToken}
/// 
/// Key requirement: every payload must include "messaging_product": "whatsapp".
/// </summary>
public sealed class WabaCloudClient
{
    private readonly HttpClient _http;
    private readonly WabaOptions _options;
    private readonly ILogger<WabaCloudClient> _logger;

    public WabaCloudClient(
        HttpClient http,
        IOptions<WabaOptions> options,
        ILogger<WabaCloudClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Send a pre-approved template message (first contact with candidate).
    /// Meta requires templates for initiating conversations (24-hour rule).
    /// </summary>
    /// <param name="phone">Phone in international format (e.g., 77011234567).</param>
    /// <param name="templateName">Template name from Meta Business Manager.</param>
    /// <param name="parameters">Template body parameters (e.g., position name, company).</param>
    /// <returns>WABA message ID (wamid.*), or null on failure.</returns>
    public async Task<string?> SendTemplateAsync(
        string phone,
        string templateName,
        List<string> parameters,
        CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhone(phone),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = _options.DefaultLanguage },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(p => new { type = "text", text = p }).ToArray()
                    }
                }
            }
        };

        return await SendRequestAsync(payload, $"template:{templateName} → {phone}", ct);
    }

    /// <summary>
    /// Send a text message within an active conversation (24-hour window after candidate replied).
    /// </summary>
    public async Task<string?> SendTextAsync(string phone, string text, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = NormalizePhone(phone),
            type = "text",
            text = new { body = text }
        };

        return await SendRequestAsync(payload, $"text → {phone}", ct);
    }

    private async Task<string?> SendRequestAsync<T>(T payload, string context, CancellationToken ct)
    {
        try
        {
            // POST to relative path "messages" — BaseAddress is set to
            // https://graph.facebook.com/v21.0/{PhoneNumberId}/ in DI registration
            var response = await _http.PostAsJsonAsync("messages", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Meta Cloud API error ({Context}): {StatusCode} — {Body}",
                    context, response.StatusCode, errorBody);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<WabaResponse>(cancellationToken: ct);
            var messageId = result?.Messages?.FirstOrDefault()?.Id;

            _logger.LogInformation("WABA sent ({Context}), messageId={MessageId}", context, messageId);
            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WABA send failed ({Context})", context);
            return null;
        }
    }

    /// <summary>
    /// WABA expects international format without '+' prefix.
    /// Handles common CIS formats (8-prefix → 7-prefix).
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // 8XXXXXXXXXX → 7XXXXXXXXXX
        if (digits.Length == 11 && digits.StartsWith('8'))
            digits = "7" + digits[1..];

        return digits;
    }
}

// ─── Cloud API Response DTOs ─────────────────────────────────────────────────

public sealed class WabaResponse
{
    [JsonPropertyName("messages")]
    public List<WabaMessageId>? Messages { get; set; }
}

public sealed class WabaMessageId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

// ─── Meta Cloud API Webhook DTOs (Nested Structure) ──────────────────────────
//
// Meta wraps the familiar messages/statuses inside:
// { "entry": [{ "changes": [{ "value": { "messages": [...], "statuses": [...] } }] }] }
//
// The inner WabaInboundMessage and WabaStatusUpdate are identical to the old 360dialog format.

public sealed class MetaWebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("entry")]
    public List<MetaWebhookEntry>? Entry { get; set; }
}

public sealed class MetaWebhookEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("changes")]
    public List<MetaWebhookChange>? Changes { get; set; }
}

public sealed class MetaWebhookChange
{
    [JsonPropertyName("value")]
    public MetaWebhookValue? Value { get; set; }

    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;
}

public sealed class MetaWebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<WabaInboundMessage>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WabaStatusUpdate>? Statuses { get; set; }
}

// ─── Inner DTOs (shared between outbound responses and webhook payloads) ─────

public sealed class WabaInboundMessage
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public WabaTextContent? Text { get; set; }
}

public sealed class WabaTextContent
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

public sealed class WabaStatusUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; set; }
}
