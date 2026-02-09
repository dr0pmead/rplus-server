using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Application.Graph;
using RPlus.Loyalty.Infrastructure.Options;
using RPlusGrpc.Meta;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class MetaGraphNodeCatalog : ILoyaltyGraphNodeCatalog
{
    private const string CacheKey = "loyalty:meta:node-templates";
    private static readonly string[] AllowedContexts = ["all", "loyalty"];

    private readonly IMemoryCache _cache;
    private readonly MetaService.MetaServiceClient _client;
    private readonly IOptionsMonitor<LoyaltyMetaOptions> _options;
    private readonly LoyaltyGraphNodeCatalog _fallback;
    private readonly ILogger<MetaGraphNodeCatalog> _logger;

    public MetaGraphNodeCatalog(
        IMemoryCache cache,
        MetaService.MetaServiceClient client,
        IOptionsMonitor<LoyaltyMetaOptions> options,
        LoyaltyGraphNodeCatalog fallback,
        ILogger<MetaGraphNodeCatalog> logger)
    {
        _cache = cache;
        _client = client;
        _options = options;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LoyaltyGraphNodeTemplate>> GetItemsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<LoyaltyGraphNodeTemplate>? cached) && cached != null)
        {
            return cached;
        }

        IReadOnlyList<LoyaltyGraphNodeTemplate> items;
        try
        {
            items = await LoadFromMetaAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load node templates from Meta. Falling back to static catalog.");
            items = await _fallback.GetItemsAsync(ct);
        }

        var ttl = Math.Max(5, _options.CurrentValue.CacheSeconds);
        _cache.Set(CacheKey, items, TimeSpan.FromSeconds(ttl));
        return items;
    }

    private async Task<IReadOnlyList<LoyaltyGraphNodeTemplate>> LoadFromMetaAsync(CancellationToken ct)
    {
        var list = await GetListAsync(ct);
        if (list == null)
        {
            return await _fallback.GetItemsAsync(ct);
        }

        var items = await GetItemsAsync(list.Id, ct);
        if (items.Count == 0)
        {
            return await _fallback.GetItemsAsync(ct);
        }

        var templates = new List<LoyaltyGraphNodeTemplate>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
                continue;

            if (string.IsNullOrWhiteSpace(item.ValueJson))
                continue;

            if (!TryParseTemplate(item.Code, item.Title, item.ValueJson!, out var template))
                continue;

            if (!IsAllowedContext(template.Contexts))
                continue;

            templates.Add(template);
        }

        return templates.Count > 0 ? templates : await _fallback.GetItemsAsync(ct);
    }

    private async Task<MetaListDto?> GetListAsync(CancellationToken ct)
    {
        var headers = BuildMetadata();
        var response = await _client.GetListByKeyAsync(
            new GetListByKeyRequest { Key = "node_templates" },
            headers,
            cancellationToken: ct);

        if (response == null || !response.Found || response.List == null)
        {
            _logger.LogWarning("Meta list lookup failed or list not found.");
            return null;
        }

        return new MetaListDto
        {
            Id = Guid.TryParse(response.List.Id, out var id) ? id : Guid.Empty,
            Key = response.List.Key,
            Title = response.List.Title
        };
    }

    private async Task<IReadOnlyList<MetaListItemDto>> GetItemsAsync(Guid listId, CancellationToken ct)
    {
        var headers = BuildMetadata();
        var response = await _client.GetListItemsAsync(
            new GetListItemsRequest { ListId = listId.ToString() },
            headers,
            cancellationToken: ct);

        if (response?.Items == null || response.Items.Count == 0)
        {
            _logger.LogWarning("Meta list items fetch returned empty.");
            return Array.Empty<MetaListItemDto>();
        }

        return response.Items.Select(item => new MetaListItemDto
        {
            Id = Guid.TryParse(item.Id, out var id) ? id : Guid.Empty,
            Code = item.Code,
            Title = item.Title,
            ValueJson = item.ValueJson
        }).ToList();
    }

    private Metadata? BuildMetadata()
    {
        var secret = _options.CurrentValue?.ServiceSecret;
        if (!string.IsNullOrWhiteSpace(secret))
        {
            return new Metadata { { "x-rplus-service-secret", secret.Trim() } };
        }

        return null;
    }

    private static bool TryParseTemplate(
        string code,
        string? title,
        string valueJson,
        out LoyaltyGraphNodeTemplate template)
    {
        template = default!;
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var category = GetString(root, "category") ?? "action";
            var description = GetString(root, "description") ?? string.Empty;
            var outputs = GetStringArray(root, "outputs");
            var requiredProps = GetStringDictionary(root, "requiredProps");
            var contexts = GetStringArray(root, "contexts");
            var advanced = GetBool(root, "advanced");
            var deprecated = GetBool(root, "deprecated");
            var version = GetInt(root, "version") ?? 1;

            template = new LoyaltyGraphNodeTemplate(
                Type: code,
                Category: category,
                Label: string.IsNullOrWhiteSpace(title) ? code : title!,
                Description: description,
                Outputs: outputs.Length == 0 ? ["next"] : outputs,
                RequiredProps: requiredProps,
                Contexts: contexts.Length == 0 ? ["all"] : contexts,
                Version: version,
                Deprecated: deprecated,
                Advanced: advanced);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAllowedContext(IReadOnlyList<string> contexts)
    {
        foreach (var ctx in contexts)
        {
            if (AllowedContexts.Contains(ctx, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static bool GetBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
            ? el.ValueKind == JsonValueKind.True
            : false;

    private static int? GetInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value)
            ? value
            : null;

    private static string[] GetStringArray(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value!);
                }
            }
        }

        return list.ToArray();
    }

    private static IReadOnlyDictionary<string, string> GetStringDictionary(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, string>();

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var value = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    dict[prop.Name] = value!;
                }
            }
        }

        return dict;
    }

    private sealed record MetaListDto
    {
        public Guid Id { get; init; }
        public string Key { get; init; } = string.Empty;
        public string? Title { get; init; }
    }

    private sealed record MetaListItemDto
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string? Title { get; init; }
        public string? ValueJson { get; init; }
    }
}
