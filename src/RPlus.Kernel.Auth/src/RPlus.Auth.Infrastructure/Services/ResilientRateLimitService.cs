// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.ResilientRateLimitService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class ResilientRateLimitService : IRedisRateLimitService
{
  private readonly IConnectionMultiplexer _redis;
  private readonly IMemoryCache _localCache;
  private readonly ILogger<ResilientRateLimitService> _logger;

  public ResilientRateLimitService(
    IConnectionMultiplexer redis,
    IMemoryCache localCache,
    ILogger<ResilientRateLimitService> logger)
  {
    this._redis = redis;
    this._localCache = localCache;
    this._logger = logger;
  }

  public async Task<(bool Allowed, int RetryAfterSeconds)> CheckRateLimitAsync(
    string key,
    int limit,
    int windowSeconds,
    CancellationToken cancellationToken = default (CancellationToken))
  {
    if (limit <= 0)
      return (true, 0);
    string localKey = "rl:l1:" + key;
    int num1;
    if (this._localCache.TryGetValue<int>((object) localKey, out num1) && num1 >= limit)
    {
      this._logger.LogWarning("L1 Rate limit triggered for {Key}", (object) key);
      return (false, windowSeconds / 2);
    }
    try
    {
      if (!this._redis.IsConnected)
      {
        this._logger.LogWarning("Redis disconnected. Falling back to L1-only protection for {Key}", (object) key);
        this.UpdateLocalCache(localKey, windowSeconds);
        return (true, 0);
      }
      IDatabase db = this._redis.GetDatabase();
      string redisKey = "ratelimit:" + key;
      long current = await db.StringIncrementAsync((RedisKey) redisKey);
      if (current == 1L)
      {
        int num2 = await db.KeyExpireAsync((RedisKey) redisKey, new TimeSpan?(TimeSpan.FromSeconds((long) windowSeconds))) ? 1 : 0;
      }
      this.UpdateLocalCache(localKey, windowSeconds);
      if (current > (long) limit)
      {
        TimeSpan? liveAsync = await db.KeyTimeToLiveAsync((RedisKey) redisKey);
        return (false, (int) Math.Ceiling(liveAsync.HasValue ? liveAsync.GetValueOrDefault().TotalSeconds : (double) windowSeconds));
      }
      db = (IDatabase) null;
      redisKey = (string) null;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Redis error in RateLimit. L1-only mode active for {Key}", (object) key);
      this.UpdateLocalCache(localKey, windowSeconds);
    }
    return (true, 0);
  }

  public async Task IncrementAsync(
    string key,
    int windowSeconds,
    CancellationToken cancellationToken = default (CancellationToken))
  {
    this.UpdateLocalCache("rl:l1:" + key, windowSeconds);
    try
    {
      if (!this._redis.IsConnected)
        return;
      IDatabase db = this._redis.GetDatabase();
      string redisKey = "ratelimit:" + key;
      long num1 = await db.StringIncrementAsync((RedisKey) redisKey);
      if (await db.KeyExistsAsync((RedisKey) redisKey))
      {
        int num2 = await db.KeyExpireAsync((RedisKey) redisKey, new TimeSpan?(TimeSpan.FromSeconds((long) windowSeconds))) ? 1 : 0;
      }
      db = (IDatabase) null;
      redisKey = (string) null;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to increment Redis rate limit for {Key}", (object) key);
    }
  }

  private void UpdateLocalCache(string localKey, int windowSeconds)
  {
    int num = this._localCache.GetOrCreate<int>((object) localKey, (Func<ICacheEntry, int>) (entry =>
    {
      entry.AbsoluteExpirationRelativeToNow = new TimeSpan?(TimeSpan.FromSeconds((long) windowSeconds));
      return 0;
    }));
    this._localCache.Set<int>((object) localKey, num + 1, TimeSpan.FromSeconds((long) windowSeconds));
  }

  public async Task ResetRateLimitAsync(string key, CancellationToken cancellationToken = default (CancellationToken))
  {
    this._localCache.Remove((object) ("rl:l1:" + key));
    try
    {
      if (!this._redis.IsConnected)
        return;
      int num = await this._redis.GetDatabase().KeyDeleteAsync((RedisKey) ("ratelimit:" + key)) ? 1 : 0;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to reset Redis rate limit for {Key}", (object) key);
    }
  }
}
