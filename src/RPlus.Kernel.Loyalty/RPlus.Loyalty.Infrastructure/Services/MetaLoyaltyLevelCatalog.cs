using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Infrastructure.Options;
using RPlusGrpc.Meta;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class MetaLoyaltyLevelCatalog : ILoyaltyLevelCatalog
{
    private const string CacheKey = "loyalty:meta:levels";
    private readonly IMemoryCache _cache;
    private readonly MetaService.MetaServiceClient _client;
    private readonly IOptionsMonitor<LoyaltyMetaOptions> _options;
    private readonly ILogger<MetaLoyaltyLevelCatalog> _logger;

    public MetaLoyaltyLevelCatalog(
        IMemoryCache cache,
        MetaService.MetaServiceClient client,
        IOptionsMonitor<LoyaltyMetaOptions> options,
        ILogger<MetaLoyaltyLevelCatalog> logger)
    {
        _cache = cache;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LoyaltyLevelEntry>> GetLevelsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<LoyaltyLevelEntry>? cached) && cached != null)
        {
            return cached;
        }

        IReadOnlyList<LoyaltyLevelEntry> levels;
        try
        {
            levels = await LoadFromMetaAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load loyalty levels from Meta.");
            levels = Array.Empty<LoyaltyLevelEntry>();
        }

        var ttl = Math.Max(5, _options.CurrentValue.CacheSeconds);
        _cache.Set(CacheKey, levels, TimeSpan.FromSeconds(ttl));
        return levels;
    }

    private async Task<IReadOnlyList<LoyaltyLevelEntry>> LoadFromMetaAsync(CancellationToken ct)
    {
        var list = await GetListAsync(ct);
        if (list == null)
        {
            return Array.Empty<LoyaltyLevelEntry>();
        }

        var items = await GetItemsAsync(list.Id, ct);
        if (items.Count == 0)
        {
            return Array.Empty<LoyaltyLevelEntry>();
        }

        var levels = new List<LoyaltyLevelEntry>();
        foreach (var item in items)
        {
            if (!item.IsActive)
                continue;

            if (TryParseLevel(item, out var level))
            {
                levels.Add(level);
            }
        }

        return levels;
    }

    private async Task<MetaListDto?> GetListAsync(CancellationToken ct)
    {
        var headers = BuildMetadata();
        var response = await _client.GetListByKeyAsync(
            new GetListByKeyRequest { Key = "loyalty_levels" },
            headers,
            cancellationToken: ct);

        if (response == null || !response.Found || response.List == null)
        {
            _logger.LogWarning("Meta list lookup failed or list not found (loyalty_levels).");
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
            return Array.Empty<MetaListItemDto>();
        }

        return response.Items.Select(item => new MetaListItemDto
        {
            Id = item.Id,
            Code = item.Code,
            Title = item.Title,
            ValueJson = item.ValueJson,
            ExternalId = item.ExternalId,
            IsActive = item.IsActive,
            Order = item.Order
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

    private static bool TryParseLevel(MetaListItemDto item, out LoyaltyLevelEntry level)
    {
        level = default!;

        var key = ResolveKey(item);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var years = 0;
        decimal discount = 0;

        if (!string.IsNullOrWhiteSpace(item.ValueJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(item.ValueJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    years = TryGetInt(root, "years");
                    discount = TryGetDecimal(root, "discount");
                }
            }
            catch
            {
                // ignore invalid JSON, keep defaults
            }
        }

        var title = string.IsNullOrWhiteSpace(item.Title) ? key : item.Title!;
        level = new LoyaltyLevelEntry(key.Trim(), title, Math.Max(0, years), discount);
        return true;
    }

    private static string ResolveKey(MetaListItemDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.ExternalId))
            return item.ExternalId!.Trim();

        if (!string.IsNullOrWhiteSpace(item.Code))
            return item.Code!.Trim();

        if (!string.IsNullOrWhiteSpace(item.Title))
            return item.Title!.Trim();

        return item.Id ?? string.Empty;
    }

    private static int TryGetInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return 0;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value))
            return value;

        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return 0;
    }

    private static decimal TryGetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return 0;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var value))
            return value;

        if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return 0;
    }

    private sealed record MetaListDto
    {
        public Guid Id { get; init; }
        public string Key { get; init; } = string.Empty;
        public string? Title { get; init; }
    }

    private sealed record MetaListItemDto
    {
        public string? Id { get; init; }
        public string? Code { get; init; }
        public string? Title { get; init; }
        public string? ValueJson { get; init; }
        public string? ExternalId { get; init; }
        public bool IsActive { get; init; }
        public int Order { get; init; }
    }
}
