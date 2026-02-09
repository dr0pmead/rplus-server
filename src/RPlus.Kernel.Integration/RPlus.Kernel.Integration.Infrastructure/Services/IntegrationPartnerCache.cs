using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using StackExchange.Redis;

namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationPartnerCache : IIntegrationPartnerCache
{
    private const string CachePrefix = "sys:integ:partner:";
    private const string MissingMarker = "__missing__";
    private readonly IntegrationDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;
    private readonly TimeSpan _missingTtl;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public IntegrationPartnerCache(
        IntegrationDbContext db,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _db = db;
        _redis = redis;

        var ttlSeconds = configuration.GetValue("Integration:Cache:PartnerTtlSeconds", 300);
        var missingSeconds = configuration.GetValue("Integration:Cache:PartnerMissingTtlSeconds", 30);
        _ttl = TimeSpan.FromSeconds(Math.Max(30, ttlSeconds));
        _missingTtl = TimeSpan.FromSeconds(Math.Max(5, missingSeconds));
    }

    public async Task<IntegrationPartnerCacheEntry?> GetAsync(Guid partnerId, CancellationToken cancellationToken)
    {
        if (partnerId == Guid.Empty)
        {
            return null;
        }

        var db = _redis.GetDatabase();
        var cacheKey = CachePrefix + partnerId.ToString("N");
        var cached = await db.StringGetAsync(cacheKey);

        if (!cached.IsNullOrEmpty)
        {
            if (cached.ToString() == MissingMarker)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<IntegrationPartnerCacheEntry>(cached.ToString(), _jsonOptions);
            }
            catch
            {
                // ignore cache errors and fall through to DB
            }
        }

        var partner = await _db.Partners
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partnerId && p.DeletedAt == null, cancellationToken);

        if (partner is null)
        {
            await db.StringSetAsync(cacheKey, MissingMarker, _missingTtl);
            return null;
        }

        var entry = new IntegrationPartnerCacheEntry(
            partner.Id,
            partner.IsActive,
            partner.IsDiscountPartner,
            partner.DiscountPartner,
            partner.AccessLevel ?? "limited",
            partner.IsDiscountPartner
                ? IntegrationPartner.DefaultDiscountProfileFieldKeys.ToList()
                : (partner.ProfileFields ?? new List<string>()));

        await SetAsync(entry, cancellationToken);
        return entry;
    }

    public async Task SetAsync(IntegrationPartnerCacheEntry entry, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var cacheKey = CachePrefix + entry.PartnerId.ToString("N");
        var payload = JsonSerializer.Serialize(entry, _jsonOptions);
        await db.StringSetAsync(cacheKey, payload, _ttl);
    }

    public async Task InvalidateAsync(Guid partnerId, CancellationToken cancellationToken)
    {
        if (partnerId == Guid.Empty)
        {
            return;
        }

        var db = _redis.GetDatabase();
        var cacheKey = CachePrefix + partnerId.ToString("N");
        await db.KeyDeleteAsync(cacheKey);
    }
}
