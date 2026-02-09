using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/admin/logs")]
public class IntegrationLogsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    public IntegrationLogsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? partnerId,
        [FromQuery] Guid? apiKeyId,
        [FromQuery] DateTime? since,
        [FromQuery] DateTime? until,
        [FromQuery] int limit = 200,
        [FromQuery] string? eventType = null,
        [FromQuery] string? action = null,
        [FromQuery] int? statusCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["source"] = "Integration",
            ["since"] = since?.ToUniversalTime().ToString("O"),
            ["until"] = until?.ToUniversalTime().ToString("O"),
            ["limit"] = limit.ToString()
        };

        var path = QueryHelpers.AddQueryString("/api/audit/events", query!);
        var client = _httpClientFactory.CreateClient("audit-http");

        var response = await client.GetFromJsonAsync<AuditEventsResponseDto>(path, cancellationToken);
        if (response?.Events is null || response.Events.Count == 0)
        {
            return Ok(new IntegrationLogsResponse(Array.Empty<IntegrationLogEntry>(), 0));
        }

        IEnumerable<AuditEventDto> filtered = response.Events;

        if (partnerId.HasValue)
            filtered = filtered.Where(e => MatchGuidMeta(e.Metadata, "partner_id", partnerId.Value));

        if (apiKeyId.HasValue)
            filtered = filtered.Where(e => MatchGuidMeta(e.Metadata, "api_key_id", apiKeyId.Value));

        if (!string.IsNullOrWhiteSpace(eventType))
            filtered = filtered.Where(e => string.Equals(e.EventType, eventType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(action))
            filtered = filtered.Where(e => e.Action.Contains(action, StringComparison.OrdinalIgnoreCase));

        if (statusCode.HasValue)
            filtered = filtered.Where(e => MatchIntMeta(e.Metadata, "status_code", statusCode.Value));

        var items = filtered
            .Select(MapToIntegrationLog)
            .ToList();

        return Ok(new IntegrationLogsResponse(items, items.Count));
    }

    private static IntegrationLogEntry MapToIntegrationLog(AuditEventDto e)
    {
        return new IntegrationLogEntry(
            e.Id,
            e.Timestamp,
            e.EventType,
            e.Severity,
            e.Actor,
            e.Action,
            e.Resource,
            GetMetaString(e.Metadata, "request_method"),
            GetMetaString(e.Metadata, "request_path"),
            GetMetaInt(e.Metadata, "status_code"),
            GetMetaLong(e.Metadata, "duration_ms"),
            GetMetaString(e.Metadata, "client_ip"),
            GetMetaGuid(e.Metadata, "partner_id"),
            GetMetaGuid(e.Metadata, "api_key_id"),
            GetMetaString(e.Metadata, "error_message")
        );
    }

    private static bool MatchGuidMeta(Dictionary<string, JsonElement> metadata, string key, Guid value)
    {
        var meta = GetMetaGuid(metadata, key);
        return meta.HasValue && meta.Value == value;
    }

    private static bool MatchIntMeta(Dictionary<string, JsonElement> metadata, string key, int value)
    {
        var meta = GetMetaInt(metadata, key);
        return meta.HasValue && meta.Value == value;
    }

    private static string? GetMetaString(Dictionary<string, JsonElement> metadata, string key)
    {
        return metadata.TryGetValue(key, out var element) ? element.ToString() : null;
    }

    private static Guid? GetMetaGuid(Dictionary<string, JsonElement> metadata, string key)
    {
        var raw = GetMetaString(metadata, key);
        return Guid.TryParse(raw, out var value) ? value : null;
    }

    private static int? GetMetaInt(Dictionary<string, JsonElement> metadata, string key)
    {
        var raw = GetMetaString(metadata, key);
        return int.TryParse(raw, out var value) ? value : null;
    }

    private static long? GetMetaLong(Dictionary<string, JsonElement> metadata, string key)
    {
        var raw = GetMetaString(metadata, key);
        return long.TryParse(raw, out var value) ? value : null;
    }

    private sealed class AuditEventsResponseDto
    {
        public List<AuditEventDto> Events { get; set; } = new();
        public int TotalCount { get; set; }
    }

    private sealed class AuditEventDto
    {
        public Guid Id { get; set; }
        public string Source { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public Dictionary<string, JsonElement> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}

public sealed record IntegrationLogEntry(
    Guid Id,
    DateTime Timestamp,
    string EventType,
    string Severity,
    string Actor,
    string Action,
    string Resource,
    string? RequestMethod,
    string? RequestPath,
    int? StatusCode,
    long? DurationMs,
    string? ClientIp,
    Guid? PartnerId,
    Guid? ApiKeyId,
    string? ErrorMessage);

public sealed record IntegrationLogsResponse(
    IReadOnlyList<IntegrationLogEntry> Items,
    int TotalCount);
