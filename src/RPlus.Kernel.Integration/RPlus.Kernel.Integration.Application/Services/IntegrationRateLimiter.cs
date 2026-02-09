// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IntegrationRateLimiter
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Microsoft.Extensions.Caching.Memory;
using RPlus.Kernel.Integration.Domain.Entities;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public class IntegrationRateLimiter : IIntegrationRateLimiter
{
  private const string ScriptText = "\nlocal key = KEYS[1]\nlocal now = tonumber(ARGV[1])\nlocal windowMs = tonumber(ARGV[2])\nlocal limit = tonumber(ARGV[3])\n\nredis.call('ZREMRANGEBYSCORE', key, 0, now - windowMs)\nlocal count = redis.call('ZCARD', key)\nif count >= limit then\n  return 0\nend\nredis.call('ZADD', key, now, now)\nredis.call('PEXPIRE', key, windowMs)\nreturn 1\n";
  private readonly IConnectionMultiplexer _redis;
  private readonly IMemoryCache _memory;
  private readonly TimeSpan _window;
  private const int DefaultLimit = 1000;

  public IntegrationRateLimiter(IConnectionMultiplexer redis, IMemoryCache memory, TimeSpan? window = null)
  {
    _redis = redis;
    _memory = memory;
    _window = window ?? TimeSpan.FromMinutes(1);
  }

  public async Task<bool> IsAllowedAsync(
    IntegrationApiKey key,
    string? routePattern,
    CancellationToken cancellationToken)
  {
    // FIXED: Removed :{minute} suffix from key pattern.
    // The Lua script already implements sliding window via ZREMRANGEBYSCORE.
    // Adding minute suffix defeated the algorithm, making it fixed window instead.
    int limit1;
    if (!string.IsNullOrEmpty(routePattern) && key.RateLimits.TryGetValue(routePattern, out limit1))
    {
      if (!await this.CheckLimitAsync($"rl:{key.Id}:{routePattern}", limit1).ConfigureAwait(false))
        return false;
    }

    int limit2;
    limit2 = key.RateLimits.TryGetValue("default", out int configured) ? configured : DefaultLimit;
    return await this.CheckLimitAsync($"rl:{key.Id}:default", limit2).ConfigureAwait(false);
  }

  private async Task<bool> CheckLimitAsync(string cacheKey, int limit)
  {
    try
    {
      if (_redis.IsConnected)
      {
        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = (long)_window.TotalMilliseconds;
        var result = (int) await db.ScriptEvaluateAsync(ScriptText, new RedisKey[] { cacheKey }, new RedisValue[]
        {
          now,
          windowMs,
          limit
        }).ConfigureAwait(false);

        return result == 1;
      }
    }
    catch
    {
      // fallback to memory
    }

    IntegrationRateLimiter.Counter counter = this._memory.GetOrCreate<IntegrationRateLimiter.Counter>((object) cacheKey, (Func<ICacheEntry, IntegrationRateLimiter.Counter>) (entry =>
    {
      entry.AbsoluteExpirationRelativeToNow = new TimeSpan?(TimeSpan.FromMinutes(1.1));
      return new IntegrationRateLimiter.Counter();
    }));
    return counter == null || Interlocked.Increment(ref counter.Value) <= limit;
  }

  private class Counter
  {
    public int Value;
  }
}
