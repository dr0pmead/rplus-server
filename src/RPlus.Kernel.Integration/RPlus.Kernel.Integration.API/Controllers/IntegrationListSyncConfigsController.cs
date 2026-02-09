using Microsoft.AspNetCore.Mvc;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Api.Services;
using RPlusGrpc.Meta;
using System.Text.Json;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/partners/{integrationId:guid}/lists/{listId:guid}/sync")]
public sealed class IntegrationListSyncConfigsController : ControllerBase
{
    private readonly IIntegrationDbContext _db;
    private readonly MetaService.MetaServiceClient _metaClient;
    private readonly IOptionsMonitor<IntegrationMetaOptions> _metaOptions;

    public IntegrationListSyncConfigsController(
        IIntegrationDbContext db,
        MetaService.MetaServiceClient metaClient,
        IOptionsMonitor<IntegrationMetaOptions> metaOptions)
    {
        _db = db;
        _metaClient = metaClient;
        _metaOptions = metaOptions;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(Guid integrationId, Guid listId, CancellationToken cancellationToken)
    {
        var config = await _db.ListSyncConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IntegrationId == integrationId && x.ListId == listId, cancellationToken);

        if (config == null)
        {
            return Ok(new
            {
                integrationId,
                listId,
                isEnabled = false,
                allowDelete = false,
                strict = false,
                mappingJson = "{}"
            });
        }

        return Ok(new
        {
            config.Id,
            config.IntegrationId,
            config.ListId,
            config.IsEnabled,
            config.AllowDelete,
            config.Strict,
            config.MappingJson,
            config.CreatedAt,
            config.UpdatedAt
        });
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpsertConfig(
        Guid integrationId,
        Guid listId,
        [FromBody] IntegrationListSyncConfigRequest request,
        CancellationToken cancellationToken)
    {
        var mappingJson = string.IsNullOrWhiteSpace(request.MappingJson)
            ? "{}"
            : request.MappingJson.Trim();

        var (isValid, mappingError) = await TryValidateMappingAsync(mappingJson, listId, cancellationToken);
        if (!isValid)
            return BadRequest(new { error = mappingError });

        var config = await _db.ListSyncConfigs
            .FirstOrDefaultAsync(x => x.IntegrationId == integrationId && x.ListId == listId, cancellationToken);

        if (config == null)
        {
            config = new IntegrationListSyncConfig
            {
                Id = Guid.NewGuid(),
                IntegrationId = integrationId,
                ListId = listId,
                IsEnabled = request.IsEnabled,
                AllowDelete = request.AllowDelete,
                Strict = request.Strict,
                MappingJson = mappingJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ListSyncConfigs.Add(config);
        }
        else
        {
            config.IsEnabled = request.IsEnabled;
            config.AllowDelete = request.AllowDelete;
            config.Strict = request.Strict;
            config.MappingJson = mappingJson;
            config.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            config.Id,
            config.IntegrationId,
            config.ListId,
            config.IsEnabled,
            config.AllowDelete,
            config.Strict,
            config.MappingJson,
            config.CreatedAt,
            config.UpdatedAt
        });
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(
        Guid integrationId,
        Guid listId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(take, 1, 200);
        var runs = await _db.ListSyncRuns
            .AsNoTracking()
            .Where(x => x.IntegrationId == integrationId && x.ListId == listId)
            .OrderByDescending(x => x.StartedAt)
            .Take(size)
            .Select(x => new
            {
                x.Id,
                x.Mode,
                x.ItemsCount,
                x.CreatedCount,
                x.UpdatedCount,
                x.DeletedCount,
                x.FailedCount,
                x.ErrorSamplesJson,
                x.StartedAt,
                x.FinishedAt,
                x.DurationMs,
                x.ApiKeyId
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items = runs, totalCount = runs.Count });
    }

    private async Task<(bool IsValid, string Error)> TryValidateMappingAsync(
        string mappingJson,
        Guid listId,
        CancellationToken cancellationToken)
    {
        var error = string.Empty;
        JsonDocument? doc = null;

        try
        {
            doc = JsonDocument.Parse(mappingJson);
        }
        catch
        {
            return (false, "invalid_mapping_json");
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return (false, "mapping_must_be_object");
        }

        var fields = await _metaClient.GetListFieldsAsync(
            new GetListFieldsRequest { ListId = listId.ToString() },
            BuildMetadata(),
            cancellationToken: cancellationToken);

        if (fields.Fields.Count == 0)
        {
            return (false, "list_fields_not_found");
        }

        var allowedKeys = new HashSet<string>(
            fields.Fields.Select(x => x.Key),
            StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var key = prop.Name.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return (false, "mapping_has_empty_key");
            }

            if (!allowedKeys.Contains(key))
            {
                return (false, $"unknown_field:{key}");
            }

            if (!IsValidMappingValue(prop.Value, out var valueError))
            {
                return (false, $"{key}:{valueError}");
            }
        }

        return (true, error);
    }

    private static bool IsValidMappingValue(JsonElement value, out string error)
    {
        error = string.Empty;
        if (value.ValueKind == JsonValueKind.String)
            return true;

        if (value.ValueKind != JsonValueKind.Object)
        {
            error = "mapping_value_invalid";
            return false;
        }

        var hasPath = value.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String;
        if (!hasPath)
        {
            error = "mapping_missing_path";
            return false;
        }

        if (value.TryGetProperty("transform", out var transformEl))
        {
            if (transformEl.ValueKind != JsonValueKind.String)
            {
                error = "transform_invalid";
                return false;
            }

            var transform = transformEl.GetString()?.Trim().ToLowerInvariant();
            if (transform is not ("tostring" or "tonumber" or "tobool"))
            {
                error = "transform_not_allowed";
                return false;
            }
        }

        return true;
    }

    private Metadata? BuildMetadata()
    {
        var secret = _metaOptions.CurrentValue.ServiceSecret;
        if (!string.IsNullOrWhiteSpace(secret))
            return new Metadata { { "x-rplus-service-secret", secret.Trim() } };

        return null;
    }
}

public sealed class IntegrationListSyncConfigRequest
{
    public bool IsEnabled { get; set; }
    public bool AllowDelete { get; set; }
    public bool Strict { get; set; }
    public string? MappingJson { get; set; }
}
