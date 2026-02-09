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

/// <summary>
/// Loads motivational tiers from Meta service (motivational_tiers list).
/// Supports any number of tiers - fully configurable via Meta.
/// </summary>
public sealed class MetaMotivationalTierCatalog : IMotivationalTierCatalog
{
    private const string CacheKey = "loyalty:meta:motivational_tiers";
    private const string ListKey = "motivational_tiers";

    private readonly IMemoryCache _cache;
    private readonly MetaService.MetaServiceClient _client;
    private readonly IOptionsMonitor<LoyaltyMetaOptions> _options;
    private readonly ILogger<MetaMotivationalTierCatalog> _logger;

    public MetaMotivationalTierCatalog(
        IMemoryCache cache,
        MetaService.MetaServiceClient client,
        IOptionsMonitor<LoyaltyMetaOptions> options,
        ILogger<MetaMotivationalTierCatalog> logger)
    {
        _cache = cache;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MotivationalTierEntry>> GetTiersAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<MotivationalTierEntry>? cached) && cached != null)
        {
            return cached;
        }

        IReadOnlyList<MotivationalTierEntry> tiers;
        try
        {
            tiers = await LoadFromMetaAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load motivational tiers from Meta.");
            tiers = Array.Empty<MotivationalTierEntry>();
        }

        var ttl = Math.Max(5, _options.CurrentValue.CacheSeconds);
        _cache.Set(CacheKey, tiers, TimeSpan.FromSeconds(ttl));
        return tiers;
    }

    private async Task<IReadOnlyList<MotivationalTierEntry>> LoadFromMetaAsync(CancellationToken ct)
    {
        var list = await GetListAsync(ct);
        if (list == null)
        {
            return Array.Empty<MotivationalTierEntry>();
        }

        var items = await GetItemsAsync(list.Id, ct);
        if (items.Count == 0)
        {
            return Array.Empty<MotivationalTierEntry>();
        }

        var tiers = new List<MotivationalTierEntry>();
        foreach (var item in items)
        {
            if (!item.IsActive)
                continue;

            if (TryParseTier(item, out var tier))
            {
                tiers.Add(tier);
            }
        }

        // Sort by MinPoints ascending for proper tier resolution
        return tiers.OrderBy(t => t.MinPoints).ThenBy(t => t.Key).ToList();
    }

    private async Task<MetaListDto?> GetListAsync(CancellationToken ct)
    {
        var headers = BuildMetadata();
        var response = await _client.GetListByKeyAsync(
            new GetListByKeyRequest { Key = ListKey },
            headers,
            cancellationToken: ct);

        if (response == null || !response.Found || response.List == null)
        {
            _logger.LogWarning("Meta list lookup failed or list not found ({ListKey}).", ListKey);
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

    private static bool TryParseTier(MetaListItemDto item, out MotivationalTierEntry tier)
    {
        tier = default!;

        // Title/Code is now the minPoints value (e.g. "100", "500", "1000")
        var minPointsStr = item.Title ?? item.Code ?? "0";
        if (!int.TryParse(minPointsStr.Trim(), out var minPoints))
        {
            minPoints = 0;
        }

        // Generate key from order or minPoints
        var key = $"tier_{item.Order}";
        if (minPoints > 0)
        {
            key = $"tier_{minPoints}";
        }

        // valueJson contains only discount
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(item.ValueJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(item.ValueJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    discount = TryGetDecimal(root, "discount");
                }
            }
            catch
            {
                // ignore invalid JSON, keep defaults
            }
        }

        tier = new MotivationalTierEntry(key, minPointsStr.Trim(), Math.Max(0, minPoints), discount);
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
