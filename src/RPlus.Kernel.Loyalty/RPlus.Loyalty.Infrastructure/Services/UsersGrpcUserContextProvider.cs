using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Infrastructure.Options;
using RPlus.Loyalty.Persistence;
using RPlusGrpc.Hr;
using RPlusGrpc.Users;
using StackExchange.Redis;
using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class UsersGrpcUserContextProvider : IUserContextProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly UsersService.UsersServiceClient _users;
    private readonly HrService.HrServiceClient _hr;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memory;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDbContextFactory<LoyaltyDbContext> _dbFactory;
    private readonly IOptionsMonitor<LoyaltyUserContextOptions> _options;
    private readonly ILogger<UsersGrpcUserContextProvider> _logger;

    public UsersGrpcUserContextProvider(
        UsersService.UsersServiceClient users,
        HrService.HrServiceClient hr,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memory,
        IDbContextFactory<LoyaltyDbContext> dbFactory,
        IOptionsMonitor<LoyaltyUserContextOptions> options,
        ILogger<UsersGrpcUserContextProvider> logger,
        IConnectionMultiplexer? redis = null)
    {
        _users = users;
        _hr = hr;
        _httpClientFactory = httpClientFactory;
        _memory = memory;
        _options = options;
        _logger = logger;
        _redis = redis;
        _dbFactory = dbFactory;
    }

    public async Task<UserContext?> GetAsync(Guid userId, DateTime asOfUtc, CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled || userId == Guid.Empty)
        {
            return null;
        }

        var cacheKey = $"userctx:{userId:N}";

        var external = await GetExternalProfileAsync(cacheKey, opts, userId, ct).ConfigureAwait(false);
        var (level, tags) = await GetLocalLoyaltyProfileAsync(userId, ct).ConfigureAwait(false);

        var hrTask = GetHrVitalStatsAsync(opts, userId, ct);
        var isBossTask = GetIsBossAsync(opts, userId, ct);

        await Task.WhenAll(hrTask, isBossTask).ConfigureAwait(false);

        var hr = hrTask.Result;
        var isBoss = isBossTask.Result;

        return ToUserContext(
            external,
            asOfUtc,
            userId,
            level,
            tags,
            hr.TenureDays,
            hr.IsBirthdayToday,
            hr.HasDisability,
            hr.ChildrenCount,
            isBoss);
    }

    private async Task<CachedUserProfile?> GetExternalProfileAsync(string cacheKey, LoyaltyUserContextOptions opts, Guid userId, CancellationToken ct)
    {
        if (_memory.TryGetValue(cacheKey, out CachedUserProfile? cached) && cached != null)
        {
            return cached;
        }

        CachedUserProfile? fromRedis = null;
        if (_redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var redisKey = opts.RedisKeyPrefix + userId.ToString("N");
                var value = await db.StringGetAsync(redisKey).ConfigureAwait(false);
                if (!value.IsNullOrEmpty)
                {
                    fromRedis = JsonSerializer.Deserialize<CachedUserProfile>(value.ToString(), SerializerOptions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed reading user context from Redis cache.");
            }
        }

        if (fromRedis != null)
        {
            _memory.Set(cacheKey, fromRedis, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
            return fromRedis;
        }

        try
        {
            var profile = await _users.GetProfileAsync(new GetProfileRequest { UserId = userId.ToString() }, cancellationToken: ct);
            var createdAt = profile.CreatedAt.ToDateTime().ToUniversalTime();
            var status = profile.Status ?? string.Empty;
            var firstName = profile.FirstName ?? string.Empty;
            var lastName = profile.LastName ?? string.Empty;
            var preferredName = profile.PreferredName ?? string.Empty;

            var entry = new CachedUserProfile
            {
                CreatedAtUtc = createdAt,
                Status = status,
                FirstName = firstName,
                LastName = lastName,
                PreferredName = preferredName
            };

            _memory.Set(cacheKey, entry, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));

            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var redisKey = opts.RedisKeyPrefix + userId.ToString("N");
                    var json = JsonSerializer.Serialize(entry);
                    await db.StringSetAsync(redisKey, json, expiry: TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds))).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed writing user context to Redis cache.");
                }
            }

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user profile from Users service for {UserId}", userId);
            return null;
        }
    }

    private async Task<(string Level, string[] Tags)> GetLocalLoyaltyProfileAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var profile = await db.ProgramProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, ct)
                .ConfigureAwait(false);

            var level = string.IsNullOrWhiteSpace(profile?.Level) ? "Base" : profile!.Level!.Trim();
            var tags = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(profile?.TagsJson))
            {
                try
                {
                    tags = JsonSerializer.Deserialize<string[]>(profile.TagsJson, SerializerOptions) ?? Array.Empty<string>();
                }
                catch
                {
                    tags = Array.Empty<string>();
                }
            }

            return (level, tags);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed reading Loyalty program profile for {UserId}", userId);
            return ("Base", Array.Empty<string>());
        }
    }

    private static UserContext ToUserContext(
        CachedUserProfile? external,
        DateTime asOfUtc,
        Guid userId,
        string level,
        string[] tags,
        int tenureDays,
        bool isBirthdayToday,
        bool hasDisability,
        int childrenCount,
        bool isBoss)
    {
        if (external == null)
        {
            return new UserContext(
                userId,
                asOfUtc,
                Status: string.Empty,
                IsVip: false,
                TenureDays: tenureDays,
                TenureYears: 0,
                IsBirthdayToday: isBirthdayToday,
                HasDisability: hasDisability,
                ChildrenCount: childrenCount,
                IsBoss: isBoss,
                Level: level,
                Tags: tags,
                FirstName: string.Empty,
                LastName: string.Empty,
                PreferredName: string.Empty);
        }

        var tenureYears = ComputeTenureYearsFromDays(tenureDays);
        var isVip = string.Equals(external.Status, "vip", StringComparison.OrdinalIgnoreCase);
        return new UserContext(
            userId,
            external.CreatedAtUtc,
            external.Status ?? string.Empty,
            isVip,
            tenureDays,
            tenureYears,
            isBirthdayToday,
            hasDisability,
            childrenCount,
            isBoss,
            level,
            tags,
            external.FirstName ?? string.Empty,
            external.LastName ?? string.Empty,
            external.PreferredName ?? string.Empty);
    }

    private static int ComputeTenureYearsFromDays(int days)
    {
        if (days <= 0)
            return 0;

        var years = days / 365.2425d;
        return (int)Math.Floor(years);
    }

    private async Task<(int TenureDays, bool IsBirthdayToday, bool HasDisability, int ChildrenCount)> GetHrVitalStatsAsync(LoyaltyUserContextOptions opts, Guid userId, CancellationToken ct)
    {
        var cacheKey = $"userctx:hr:{userId:N}";
        if (_memory.TryGetValue(cacheKey, out (int TenureDays, bool IsBirthdayToday, bool HasDisability, int ChildrenCount) cached))
        {
            return cached;
        }

        try
        {
            Grpc.Core.Metadata? headers = null;
            var secret = (opts.HrSharedSecret ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(secret))
            {
                headers = new Grpc.Core.Metadata { { "x-rplus-service-secret", secret } };
            }

            var response = await _hr.GetVitalStatsAsync(
                new GetVitalStatsRequest { UserId = userId.ToString() },
                headers: headers,
                cancellationToken: ct);

            var value = (response.TenureDays, response.IsBirthdayToday, response.HasDisability, response.ChildrenCount);
            _memory.Set(cacheKey, value, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch HR vital stats for {UserId}", userId);
            var value = (0, false, false, 0);
            _memory.Set(cacheKey, value, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
            return value;
        }
    }

    private async Task<bool> GetIsBossAsync(LoyaltyUserContextOptions opts, Guid userId, CancellationToken ct)
    {
        var cacheKey = $"userctx:orgboss:{userId:N}";
        if (_memory.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Organization");
            var secret = (opts.OrganizationSharedSecret ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(secret))
            {
                client.DefaultRequestHeaders.Remove("x-rplus-service-secret");
                client.DefaultRequestHeaders.Add("x-rplus-service-secret", secret);
            }

            using var response = await client.GetAsync($"api/organization/users/{userId:D}/assignments", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _memory.Set(cacheKey, false, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _memory.Set(cacheKey, false, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                _memory.Set(cacheKey, false, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
                return false;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                if (!item.TryGetProperty("roleCode", out var rc) && !item.TryGetProperty("RoleCode", out rc))
                    continue;

                var roleCode = (rc.GetString() ?? string.Empty).Trim();
                if (string.Equals(roleCode, "org.head", StringComparison.OrdinalIgnoreCase))
                {
                    _memory.Set(cacheKey, true, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
                    return true;
                }
            }

            _memory.Set(cacheKey, false, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve Organization boss flag for {UserId}", userId);
            _memory.Set(cacheKey, false, TimeSpan.FromSeconds(Math.Max(5, opts.CacheSeconds)));
            return false;
        }
    }

    private sealed class CachedUserProfile
    {
        public DateTime CreatedAtUtc { get; set; }

        public string Status { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string PreferredName { get; set; } = string.Empty;

    }
}
