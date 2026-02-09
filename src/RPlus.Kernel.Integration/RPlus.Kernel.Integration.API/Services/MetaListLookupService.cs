using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlusGrpc.Meta;
using System.Text.Json;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IMetaListLookupService
{
    Task<IReadOnlyList<MetaListLookupItem>> GetListAsync(string listKey, CancellationToken cancellationToken);
}

public sealed record MetaListLookupItem(
    string Code,
    string Title,
    string? ExternalId,
    JsonElement? Value);

public sealed class MetaListLookupService : IMetaListLookupService
{
    private readonly MetaService.MetaServiceClient _metaClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MetaListLookupService> _logger;
    private readonly IOptionsMonitor<IntegrationMetaOptions> _metaOptions;

    public MetaListLookupService(
        MetaService.MetaServiceClient metaClient,
        IMemoryCache cache,
        ILogger<MetaListLookupService> logger,
        IOptionsMonitor<IntegrationMetaOptions> metaOptions)
    {
        _metaClient = metaClient;
        _cache = cache;
        _logger = logger;
        _metaOptions = metaOptions;
    }

    public async Task<IReadOnlyList<MetaListLookupItem>> GetListAsync(string listKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(listKey))
            return Array.Empty<MetaListLookupItem>();

        var cacheKey = $"meta_list_lookup::{listKey.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MetaListLookupItem>? cached) && cached != null)
            return cached;

        var items = await LoadAsync(listKey, cancellationToken);
        _cache.Set(cacheKey, items, TimeSpan.FromMinutes(5));
        return items;
    }

    private async Task<IReadOnlyList<MetaListLookupItem>> LoadAsync(string listKey, CancellationToken cancellationToken)
    {
        try
        {
            var listResponse = await _metaClient.GetListByKeyAsync(
                new GetListByKeyRequest { Key = listKey },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            if (!listResponse.Found)
                return Array.Empty<MetaListLookupItem>();

            var listItems = await _metaClient.GetListItemsAsync(
                new GetListItemsRequest { ListId = listResponse.List.Id },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            var items = new List<MetaListLookupItem>();
            foreach (var item in listItems.Items)
            {
                items.Add(new MetaListLookupItem(
                    Code: item.Code ?? string.Empty,
                    Title: item.Title ?? string.Empty,
                    ExternalId: string.IsNullOrWhiteSpace(item.ExternalId) ? null : item.ExternalId,
                    Value: ParseJson(item.ValueJson)));
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load list {ListKey} from Meta", listKey);
            return Array.Empty<MetaListLookupItem>();
        }
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private Metadata? BuildMetadata()
    {
        var secret = _metaOptions.CurrentValue.ServiceSecret;
        if (!string.IsNullOrWhiteSpace(secret))
            return new Metadata { { "x-rplus-service-secret", secret.Trim() } };

        return null;
    }
}
