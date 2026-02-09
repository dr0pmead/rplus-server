using System.Text.Json;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlusGrpc.Meta;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IIntegrationListSyncService
{
    Task<IntegrationListSyncResult> SyncAsync(
        string integrationKey,
        Guid listId,
        IntegrationListSyncRequest request,
        CancellationToken ct);
}

public sealed record IntegrationListSyncResult(
    bool Success,
    int StatusCode,
    string Error,
    IntegrationListSyncResponse? Response)
{
    public static IntegrationListSyncResult Ok(IntegrationListSyncResponse response)
        => new(true, StatusCodes.Status200OK, string.Empty, response);

    public static IntegrationListSyncResult Fail(int statusCode, string error)
        => new(false, statusCode, error, null);
}

public sealed class IntegrationListSyncService : IIntegrationListSyncService
{
    private readonly IIntegrationDbContext _db;
    private readonly IPartnerApiKeyValidator _apiKeyValidator;
    private readonly IAccessIntegrationPermissionService _permissionService;
    private readonly MetaService.MetaServiceClient _metaClient;
    private readonly IOptionsMonitor<IntegrationMetaOptions> _metaOptions;
    private readonly IOptionsMonitor<IntegrationListSyncOptions> _syncOptions;

    public IntegrationListSyncService(
        IIntegrationDbContext db,
        IPartnerApiKeyValidator apiKeyValidator,
        IAccessIntegrationPermissionService permissionService,
        MetaService.MetaServiceClient metaClient,
        IOptionsMonitor<IntegrationMetaOptions> metaOptions,
        IOptionsMonitor<IntegrationListSyncOptions> syncOptions)
    {
        _db = db;
        _apiKeyValidator = apiKeyValidator;
        _permissionService = permissionService;
        _metaClient = metaClient;
        _metaOptions = metaOptions;
        _syncOptions = syncOptions;
    }

    public async Task<IntegrationListSyncResult> SyncAsync(
        string integrationKey,
        Guid listId,
        IntegrationListSyncRequest request,
        CancellationToken ct)
    {
        if (request.Items.Count == 0)
            return IntegrationListSyncResult.Fail(StatusCodes.Status400BadRequest, "empty_items");

        var syncOptions = _syncOptions.CurrentValue;
        if (syncOptions.MaxItems > 0 && request.Items.Count > syncOptions.MaxItems)
            return IntegrationListSyncResult.Fail(StatusCodes.Status413PayloadTooLarge, "batch_too_large");

        if (syncOptions.MaxPayloadBytes > 0)
        {
            long totalBytes = 0;
            foreach (var item in request.Items)
            {
                if (item.Payload.ValueKind != JsonValueKind.Undefined && item.Payload.ValueKind != JsonValueKind.Null)
                {
                    totalBytes += item.Payload.GetRawText().Length;
                    if (totalBytes > syncOptions.MaxPayloadBytes)
                        return IntegrationListSyncResult.Fail(StatusCodes.Status413PayloadTooLarge, "payload_too_large");
                }
            }
        }

        var keyResult = await _apiKeyValidator.ValidateAsync(integrationKey, ct);
        if (!keyResult.Success || keyResult.Context?.Metadata == null)
            return IntegrationListSyncResult.Fail(keyResult.StatusCode, keyResult.Error);

        var metadata = keyResult.Context.Metadata;
        if (!metadata.PartnerId.HasValue)
            return IntegrationListSyncResult.Fail(StatusCodes.Status401Unauthorized, "invalid_integration_key");

        var integrationId = metadata.PartnerId.Value;

        var requiredPermission = syncOptions.RequiredPermission;
        if (syncOptions.EnforcePermission && !string.IsNullOrWhiteSpace(requiredPermission))
        {
            var hasPermission = await _permissionService.HasPermissionAsync(
                metadata.KeyId,
                requiredPermission,
                ct);
            if (!hasPermission)
                return IntegrationListSyncResult.Fail(StatusCodes.Status403Forbidden, "permission_denied");
        }

        var config = await _db.ListSyncConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IntegrationId == integrationId && x.ListId == listId, ct);

        if (config == null || !config.IsEnabled)
            return IntegrationListSyncResult.Fail(StatusCodes.Status403Forbidden, "sync_not_enabled");

        var mode = NormalizeMode(request.Mode);
        if (mode == "delete" && !config.AllowDelete)
            return IntegrationListSyncResult.Fail(StatusCodes.Status403Forbidden, "delete_not_allowed");

        var startedAt = DateTime.UtcNow;
        var response = new IntegrationListSyncResponse
        {
            RunId = Guid.NewGuid(),
            ListId = listId,
            Mode = mode
        };

        var mapping = ParseMapping(config.MappingJson);
        if (mapping == null && !string.IsNullOrWhiteSpace(config.MappingJson))
            return IntegrationListSyncResult.Fail(StatusCodes.Status400BadRequest, "invalid_mapping");

        var results = new List<IntegrationListSyncItemResult>();

        if (request.DryRun)
        {
            foreach (var item in request.Items)
            {
                var externalId = NormalizeExternalId(item.ExternalId);
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = string.Empty,
                        Status = "failed",
                        Error = "missing_external_id"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                var payload = item.Payload;
                if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = externalId,
                        Status = "failed",
                        Error = "missing_payload"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                if (payload.ValueKind != JsonValueKind.Object)
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = externalId,
                        Status = "failed",
                        Error = "invalid_payload"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                if (!TryMapPayload(payload, mapping, out _, out _, out var error))
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = externalId,
                        Status = "failed",
                        Error = error ?? "mapping_failed"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                results.Add(new IntegrationListSyncItemResult
                {
                    ExternalId = externalId,
                    Status = "validated"
                });
            }

            response.ItemsCount = request.Items.Count;
            response.Results = results;
            return IntegrationListSyncResult.Ok(response);
        }

        if (mode == "delete")
        {
            var externalIds = request.Items
                .Select(x => NormalizeExternalId(x.ExternalId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (externalIds.Count == 0)
                return IntegrationListSyncResult.Fail(StatusCodes.Status400BadRequest, "missing_external_id");

            var deleteResponse = await _metaClient.DeleteListItemsAsync(
                new DeleteListItemsRequest
                {
                    ListId = listId.ToString(),
                    ExternalId = { externalIds }
                },
                BuildMetadata(),
                cancellationToken: ct);

            foreach (var item in deleteResponse.Results)
            {
                var status = item.Status;
                var isFailed = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);
                var isDeleted = string.Equals(status, "deleted", StringComparison.OrdinalIgnoreCase);

                if (isFailed)
                {
                    response.Failed++;
                }
                else if (isDeleted)
                {
                    response.Deleted++;
                }

                results.Add(new IntegrationListSyncItemResult
                {
                    ExternalId = item.ExternalId,
                    Status = status,
                    Error = item.Error
                });
            }

            response.ItemsCount = externalIds.Count;
        }
        else
        {
            var upsertRequest = new UpsertListItemsRequest
            {
                ListId = listId.ToString(),
                Strict = config.Strict
            };

            foreach (var item in request.Items)
            {
                var externalId = NormalizeExternalId(item.ExternalId);
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = string.Empty,
                        Status = "failed",
                        Error = "missing_external_id"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                var payload = item.Payload;
                if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = externalId,
                        Status = "failed",
                        Error = "missing_payload"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                if (payload.ValueKind != JsonValueKind.Object)
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = externalId,
                        Status = "failed",
                        Error = "invalid_payload"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                if (!TryMapPayload(payload, mapping, out var mapped, out var title, out var error))
                {
                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = externalId,
                        Status = "failed",
                        Error = error ?? "mapping_failed"
                    });
                    response.Failed++;
                    if (config.Strict)
                        break;
                    continue;
                }

                var valueJson = mapped != null
                    ? JsonSerializer.Serialize(mapped)
                    : payload.GetRawText();

                upsertRequest.Items.Add(new UpsertListItem
                {
                    ExternalId = externalId,
                    Code = externalId,
                    Title = title ?? externalId,
                    ValueJson = valueJson,
                    IsActive = true,
                    Order = 0
                });
            }

            if (upsertRequest.Items.Count == 0 && response.Failed == 0)
                return IntegrationListSyncResult.Fail(StatusCodes.Status400BadRequest, "empty_items");

            if (upsertRequest.Items.Count > 0)
            {
                var upsertResponse = await _metaClient.UpsertListItemsAsync(
                    upsertRequest,
                    BuildMetadata(),
                    cancellationToken: ct);

                foreach (var item in upsertResponse.Results)
                {
                    var status = item.Status;
                    if (string.Equals(status, "created", StringComparison.OrdinalIgnoreCase))
                        response.Created++;
                    else if (string.Equals(status, "updated", StringComparison.OrdinalIgnoreCase))
                        response.Updated++;
                    else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                        response.Failed++;

                    results.Add(new IntegrationListSyncItemResult
                    {
                        ExternalId = item.ExternalId,
                        Status = status,
                        Error = item.Error
                    });
                }

                response.ItemsCount = upsertRequest.Items.Count;
            }
        }

        response.Results = results;

        await SaveRunAsync(
            response,
            integrationId,
            metadata.KeyId,
            startedAt,
            ct);

        return IntegrationListSyncResult.Ok(response);
    }

    private async Task SaveRunAsync(
        IntegrationListSyncResponse response,
        Guid integrationId,
        Guid? apiKeyId,
        DateTime startedAt,
        CancellationToken ct)
    {
        var finishedAt = DateTime.UtcNow;
        var duration = (long)Math.Max(0, (finishedAt - startedAt).TotalMilliseconds);

        var errorSamples = response.Results
            .Where(x => !string.IsNullOrWhiteSpace(x.Error))
            .Take(20)
            .Select(x => new { x.ExternalId, x.Error })
            .ToList();

        var run = new IntegrationListSyncRun
        {
            Id = response.RunId,
            IntegrationId = integrationId,
            ListId = response.ListId,
            ApiKeyId = apiKeyId,
            Mode = response.Mode,
            ItemsCount = response.ItemsCount,
            CreatedCount = response.Created,
            UpdatedCount = response.Updated,
            DeletedCount = response.Deleted,
            FailedCount = response.Failed,
            ErrorSamplesJson = errorSamples.Count == 0 ? null : JsonSerializer.Serialize(errorSamples),
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = duration
        };

        _db.Set<IntegrationListSyncRun>().Add(run);
        await _db.SaveChangesAsync(ct);
    }

    private Metadata? BuildMetadata()
    {
        var secret = _metaOptions.CurrentValue.ServiceSecret;
        if (!string.IsNullOrWhiteSpace(secret))
            return new Metadata { { "x-rplus-service-secret", secret.Trim() } };

        return null;
    }

    private static string NormalizeMode(string? value)
    {
        var s = (value ?? "upsert").Trim().ToLowerInvariant();
        return s == "delete" ? "delete" : "upsert";
    }

    private static string? NormalizeExternalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private static JsonElement? ParseMapping(string? mappingJson)
    {
        if (string.IsNullOrWhiteSpace(mappingJson))
            return null;
        try
        {
            var doc = JsonDocument.Parse(mappingJson);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static bool TryMapPayload(
        JsonElement payload,
        JsonElement? mapping,
        out Dictionary<string, object?>? mapped,
        out string? title,
        out string? error)
    {
        mapped = null;
        title = null;
        error = null;

        if (mapping == null)
        {
            mapped = null;
            title = ExtractString(payload, "title") ?? ExtractString(payload, "name");
            return true;
        }

        if (mapping.Value.ValueKind != JsonValueKind.Object)
        {
            error = "invalid_mapping";
            return false;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in mapping.Value.EnumerateObject())
        {
            var key = prop.Name;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (string.Equals(key, "title", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedTitle = ResolveMappingValue(payload, prop.Value, out _);
                title = resolvedTitle?.ToString();
                continue;
            }

            var value = ResolveMappingValue(payload, prop.Value, out var found);
            if (!found)
            {
                var defaultValue = ResolveDefault(prop.Value);
                if (defaultValue != null)
                    result[key] = defaultValue;
                continue;
            }

            result[key] = value;
        }

        mapped = result;
        if (string.IsNullOrWhiteSpace(title))
            title = ExtractString(payload, "title") ?? ExtractString(payload, "name");

        return true;
    }

    private static object? ResolveMappingValue(JsonElement payload, JsonElement mappingValue, out bool found)
    {
        found = false;
        if (mappingValue.ValueKind == JsonValueKind.String)
        {
            var path = mappingValue.GetString() ?? string.Empty;
            return ExtractPathValue(payload, path, out found);
        }

        if (mappingValue.ValueKind == JsonValueKind.Object)
        {
            if (mappingValue.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
            {
                var path = pathEl.GetString() ?? string.Empty;
                var value = ExtractPathValue(payload, path, out found);
                if (!found)
                    return null;

                if (mappingValue.TryGetProperty("transform", out var transformEl) && transformEl.ValueKind == JsonValueKind.String)
                {
                    return ApplyTransform(value, transformEl.GetString());
                }

                return value;
            }
        }

        return null;
    }

    private static object? ResolveDefault(JsonElement mappingValue)
    {
        if (mappingValue.ValueKind == JsonValueKind.Object &&
            mappingValue.TryGetProperty("default", out var def))
        {
            return ConvertJsonElement(def);
        }

        return null;
    }

    private static object? ApplyTransform(object? value, string? transform)
    {
        if (value == null)
            return null;

        var t = (transform ?? string.Empty).Trim().ToLowerInvariant();
        switch (t)
        {
            case "tostring":
                return value.ToString();
            case "tonumber":
                if (value is double d)
                    return d;
                if (value is int i)
                    return (double)i;
                if (double.TryParse(value.ToString(), out var parsed))
                    return parsed;
                return null;
            case "tobool":
                if (value is bool b)
                    return b;
                if (bool.TryParse(value.ToString(), out var bParsed))
                    return bParsed;
                return null;
            default:
                return value;
        }
    }

    private static object? ExtractPathValue(JsonElement payload, string? path, out bool found)
    {
        found = false;
        var normalized = (path ?? string.Empty).Trim();
        if (normalized.StartsWith("payload.", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring("payload.".Length);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = payload;
        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out var next))
                return null;
            current = next;
        }

        found = true;
        return ConvertJsonElement(current);
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                    return l;
                if (element.TryGetDouble(out var d))
                    return d;
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return JsonSerializer.Deserialize<object>(element.GetRawText());
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static string? ExtractString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return null;

        if (!payload.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return null;

        return el.GetString();
    }
}

public sealed class IntegrationListSyncRequest
{
    public string Mode { get; set; } = "upsert";
    public bool DryRun { get; set; }
    public List<IntegrationListSyncItem> Items { get; set; } = new();
}

public sealed class IntegrationListSyncItem
{
    public string? ExternalId { get; set; }
    public JsonElement Payload { get; set; }
}

public sealed class IntegrationListSyncResponse
{
    public Guid RunId { get; set; }
    public Guid ListId { get; set; }
    public string Mode { get; set; } = "upsert";
    public int ItemsCount { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Failed { get; set; }
    public List<IntegrationListSyncItemResult> Results { get; set; } = new();
}

public sealed class IntegrationListSyncItemResult
{
    public string ExternalId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}
