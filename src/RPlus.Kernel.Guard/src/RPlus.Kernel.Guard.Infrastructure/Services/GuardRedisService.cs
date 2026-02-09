using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using RPlus.Kernel.Guard.Domain.Services;
using RPlus.SDK.Security.Enums;
using RPlus.SDK.Security.Models;
using RPlus.SDK.Security.Helpers;

namespace RPlus.Kernel.Guard.Infrastructure.Services;

public class GuardRedisService : IGuardStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public GuardRedisService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    public async Task<(long count, long ttl)> IncrementRateLimitAsync(string key, int windowSeconds)
    {
        // Use helper? Or raw key? Pipeline passes "ip", Helper expects "subject, route".
        // Pipeline logic was: await _stateStore.IncrementRateLimitAsync(ip, 10);
        // So keys generation happens here or passed in?
        // Implementation:
        var redisKey = GuardRedisKeys.Rate(key, "global"); // Simple default for now
        
        var count = await _db.StringIncrementAsync(redisKey);
        var ttl = await _db.KeyTimeToLiveAsync(redisKey);
        
        if (count == 1 || ttl == null)
        {
            await _db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(windowSeconds));
            return (count, windowSeconds * 1000);
        }

        return (count, (long)(ttl.Value.TotalSeconds));
    }

    public async Task SetThreatLevelAsync(string ip, ThreatLevel level, TimeSpan ttl)
    {
        var key = GuardRedisKeys.Threat(ip);
        await _db.StringSetAsync(key, (int)level, ttl);
    }

    public async Task<ThreatLevel> GetThreatLevelAsync(string ip)
    {
        var key = GuardRedisKeys.Threat(ip);
        var val = await _db.StringGetAsync(key);
        if (val.HasValue && int.TryParse(val.ToString(), out int level))
        {
            return (ThreatLevel)level;
        }
        return ThreatLevel.Low;
    }

    public async Task BlockSubjectAsync(string key, TimeSpan duration, string reason)
    {
        var redisKey = GuardRedisKeys.Block(key);
        await _db.StringSetAsync(redisKey, reason, duration);
    }

    public async Task<bool> IsBlockedAsync(string key)
    {
         var redisKey = GuardRedisKeys.Block(key);
         return await _db.KeyExistsAsync(redisKey);
    }

    public async Task SetChallengeAsync(SecurityChallenge challenge)
    {
        var key = GuardRedisKeys.Challenge(challenge.ChallengeId);
        var json = JsonSerializer.Serialize(challenge);
        var ttl = challenge.ExpiresAt - DateTime.UtcNow;
        if (ttl > TimeSpan.Zero)
            await _db.StringSetAsync(key, json, ttl);
    }

    public async Task<SecurityChallenge?> GetChallengeAsync(string challengeId)
    {
        var key = GuardRedisKeys.Challenge(challengeId);
        var val = await _db.StringGetAsync(key);
        if (val.HasValue)
        {
            return JsonSerializer.Deserialize<SecurityChallenge>(val.ToString());
        }
        return null;
    }

    public async Task RemoveChallengeAsync(string challengeId)
    {
        var key = GuardRedisKeys.Challenge(challengeId);
        await _db.KeyDeleteAsync(key);
    }
}
