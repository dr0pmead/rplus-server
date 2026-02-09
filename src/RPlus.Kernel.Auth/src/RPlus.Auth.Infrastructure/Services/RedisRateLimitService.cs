// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.RedisRateLimitService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class RedisRateLimitService : IRedisRateLimitService
{
  private readonly IConnectionMultiplexer _redis;
  private readonly ILogger<RedisRateLimitService> _logger;

  public RedisRateLimitService(IConnectionMultiplexer redis, ILogger<RedisRateLimitService> logger)
  {
    this._redis = redis;
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
    try
    {
      if (!this._redis.IsConnected)
      {
        this._logger.LogWarning("Redis is not connected. Attempting to reconnect...");
        await Task.Delay(100, cancellationToken);
        if (!this._redis.IsConnected)
        {
          this._logger.LogWarning("Redis is still not connected after retry. Allowing request without rate limiting.");
          return (true, 0);
        }
      }
      IDatabase db = this._redis.GetDatabase();
      string redisKey = "ratelimit:" + key;
      long current = await db.StringIncrementAsync((RedisKey) redisKey);
      if (current == 1L)
      {
        int num = await db.KeyExpireAsync((RedisKey) redisKey, new TimeSpan?(TimeSpan.FromSeconds((long) windowSeconds))) ? 1 : 0;
      }
      if (current <= (long) limit)
        return (true, 0);
      TimeSpan? liveAsync = await db.KeyTimeToLiveAsync((RedisKey) redisKey);
      double a = liveAsync.HasValue ? liveAsync.GetValueOrDefault().TotalSeconds : (double) windowSeconds;
      this._logger.LogWarning("Rate limit exceeded for key {Key}. Current: {Current}, Limit: {Limit}, RetryAfter: {RetryAfter}s", (object) key, (object) current, (object) limit, (object) a);
      return (false, (int) Math.Ceiling(a));
    }
    catch (RedisConnectionException ex)
    {
      this._logger.LogError((Exception) ex, "Redis connection error while checking rate limit for key {Key}. Allowing request.", (object) key);
      return (true, 0);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Unexpected error checking rate limit in Redis for key {Key}. Allowing request.", (object) key);
      return (true, 0);
    }
  }

  public async Task IncrementAsync(
    string key,
    int windowSeconds,
    CancellationToken cancellationToken = default (CancellationToken))
  {
    try
    {
      if (!this._redis.IsConnected)
      {
        this._logger.LogWarning("Redis is not connected. Skipping increment for key {Key}.", (object) key);
      }
      else
      {
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
    }
    catch (RedisConnectionException ex)
    {
      this._logger.LogError((Exception) ex, "Redis connection error while incrementing rate limit for key {Key}. Skipping.", (object) key);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Unexpected error incrementing rate limit in Redis for key {Key}. Skipping.", (object) key);
    }
  }

  public async Task ResetRateLimitAsync(string key, CancellationToken cancellationToken = default (CancellationToken))
  {
    try
    {
      if (!this._redis.IsConnected)
        return;
      int num = await this._redis.GetDatabase().KeyDeleteAsync((RedisKey) ("ratelimit:" + key)) ? 1 : 0;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error resetting rate limit for key {Key}", (object) key);
    }
  }
}
