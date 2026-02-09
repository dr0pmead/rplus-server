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
using RPlus.Loyalty.Infrastructure.Jobs;
using RPlus.Loyalty.Infrastructure.Options;
using RPlusGrpc.Meta;

namespace RPlus.Loyalty.Infrastructure.Services;

/// <summary>
/// Loads leaderboard rewards from Meta service.
/// Expected lists: leaderboard_rewards_monthly, leaderboard_rewards_yearly
/// </summary>
public sealed class MetaLeaderboardRewardCatalog : ILeaderboardRewardCatalog
{
    private const string CacheKeyMonthly = "loyalty:meta:leaderboard_rewards_monthly";
    private const string CacheKeyYearly = "loyalty:meta:leaderboard_rewards_yearly";
    private const string ListKeyMonthly = "leaderboard_rewards_monthly";
    private const string ListKeyYearly = "leaderboard_rewards_yearly";

    private readonly IMemoryCache _cache;
    private readonly MetaService.MetaServiceClient _client;
    private readonly IOptionsMonitor<LoyaltyMetaOptions> _options;
    private readonly ILogger<MetaLeaderboardRewardCatalog> _logger;

    public MetaLeaderboardRewardCatalog(
        IMemoryCache cache,
        MetaService.MetaServiceClient client,
        IOptionsMonitor<LoyaltyMetaOptions> options,
        ILogger<MetaLeaderboardRewardCatalog> logger)
    {
        _cache = cache;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<List<LeaderboardRewardConfig>> GetMonthlyRewardsAsync(CancellationToken ct = default)
    {
        return await GetRewardsAsync(CacheKeyMonthly, ListKeyMonthly, ct);
    }

    public async Task<List<LeaderboardRewardConfig>> GetYearlyRewardsAsync(CancellationToken ct = default)
    {
        return await GetRewardsAsync(CacheKeyYearly, ListKeyYearly, ct);
    }

    private async Task<List<LeaderboardRewardConfig>> GetRewardsAsync(string cacheKey, string listKey, CancellationToken ct)
    {
        if (_cache.TryGetValue(cacheKey, out List<LeaderboardRewardConfig>? cached) && cached != null)
        {
            return cached;
        }

        List<LeaderboardRewardConfig> rewards;
        try
        {
            rewards = await LoadFromMetaAsync(listKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load leaderboard rewards from Meta ({ListKey}).", listKey);
            rewards = new List<LeaderboardRewardConfig>();
        }

        var ttl = Math.Max(5, _options.CurrentValue.CacheSeconds);
        _cache.Set(cacheKey, rewards, TimeSpan.FromSeconds(ttl));
        return rewards;
    }

    private async Task<List<LeaderboardRewardConfig>> LoadFromMetaAsync(string listKey, CancellationToken ct)
    {
        var list = await GetListAsync(listKey, ct);
        if (list == null)
        {
            return new List<LeaderboardRewardConfig>();
        }

        var items = await GetItemsAsync(list.Value, ct);
        if (items.Count == 0)
        {
            return new List<LeaderboardRewardConfig>();
        }

        var rewards = new List<LeaderboardRewardConfig>();
        foreach (var item in items)
        {
            if (!item.IsActive)
                continue;

            if (TryParseReward(item, out var reward))
            {
                rewards.Add(reward);
            }
        }

        return rewards.OrderBy(r => r.Rank).ToList();
    }

    private async Task<Guid?> GetListAsync(string listKey, CancellationToken ct)
    {
        var headers = BuildMetadata();
        var response = await _client.GetListByKeyAsync(
            new GetListByKeyRequest { Key = listKey },
            headers,
            cancellationToken: ct);

        if (response == null || !response.Found || response.List == null)
        {
            _logger.LogDebug("Meta list '{ListKey}' not found.", listKey);
            return null;
        }

        return Guid.TryParse(response.List.Id, out var id) ? id : null;
    }

    private async Task<IReadOnlyList<MetaRewardItem>> GetItemsAsync(Guid listId, CancellationToken ct)
    {
        var headers = BuildMetadata();
        var response = await _client.GetListItemsAsync(
            new GetListItemsRequest { ListId = listId.ToString() },
            headers,
            cancellationToken: ct);

        if (response?.Items == null || response.Items.Count == 0)
        {
            return Array.Empty<MetaRewardItem>();
        }

        return response.Items.Select(item => new MetaRewardItem
        {
            Code = item.Code,
            Title = item.Title,
            ValueJson = item.ValueJson,
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

    /// <summary>
    /// Parse reward from Meta item.
    /// Expected format:
    /// - Title/Code: rank number (1, 2, 3)
    /// - ValueJson: { "reward_type": "discount|points|prize", "value": "3" or "500" or "iPhone 16" }
    /// </summary>
    private static bool TryParseReward(MetaRewardItem item, out LeaderboardRewardConfig reward)
    {
        reward = default!;

        // Parse rank from Title or Code
        var rankStr = item.Title ?? item.Code ?? "0";
        if (!int.TryParse(rankStr.Trim(), out var rank) || rank <= 0)
        {
            return false;
        }

        // Parse valueJson
        string rewardType = "points";
        string value = "0";

        if (!string.IsNullOrWhiteSpace(item.ValueJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(item.ValueJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("reward_type", out var rt))
                        rewardType = rt.GetString() ?? "points";

                    if (root.TryGetProperty("value", out var v))
                        value = v.ToString();
                }
            }
            catch
            {
                // Use defaults
            }
        }

        reward = new LeaderboardRewardConfig(rank, rewardType, value);
        return true;
    }

    private sealed record MetaRewardItem
    {
        public string? Code { get; init; }
        public string? Title { get; init; }
        public string? ValueJson { get; init; }
        public bool IsActive { get; init; }
        public int Order { get; init; }
    }
}
