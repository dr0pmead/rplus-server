// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Integration.IntegRateLimiter
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Infrastructure.Integration;

public sealed class IntegRateLimiter : IIntegRateLimiter
{
  private const string ScriptText = "\nlocal key = KEYS[1]\nlocal now = tonumber(ARGV[1])\nlocal windowMs = tonumber(ARGV[2])\nlocal limit = tonumber(ARGV[3])\n\nredis.call('ZREMRANGEBYSCORE', key, 0, now - windowMs)\nlocal count = redis.call('ZCARD', key)\nif count >= limit then\n  return 0\nend\nredis.call('ZADD', key, now, now)\nredis.call('PEXPIRE', key, windowMs)\nreturn 1\n";
  private readonly IConnectionMultiplexer _redis;
  private readonly TimeSpan _window;

  public IntegRateLimiter(IConnectionMultiplexer redis, TimeSpan? window = null)
  {
    this._redis = redis;
    this._window = window ?? TimeSpan.FromMinutes(1L);
  }

  public async Task<bool> IsAllowedAsync(
    ApiKeyMetadata metadata,
    string scope,
    CancellationToken cancellationToken)
  {
    if (!metadata.IsActive)
      return false;
    int num;
    if (!metadata.RateLimits.TryGetValue(scope, out num))
      return true;
    IDatabase database = this._redis.GetDatabase();
    long timeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    long totalMilliseconds = (long) this._window.TotalMilliseconds;
    string str = $"rate:{metadata.Env}:{metadata.PartnerId}:{metadata.KeyId}:{scope}";
    return (int) await database.ScriptEvaluateAsync("\nlocal key = KEYS[1]\nlocal now = tonumber(ARGV[1])\nlocal windowMs = tonumber(ARGV[2])\nlocal limit = tonumber(ARGV[3])\n\nredis.call('ZREMRANGEBYSCORE', key, 0, now - windowMs)\nlocal count = redis.call('ZCARD', key)\nif count >= limit then\n  return 0\nend\nredis.call('ZADD', key, now, now)\nredis.call('PEXPIRE', key, windowMs)\nreturn 1\n", new RedisKey[1]
    {
      (RedisKey) str
    }, new RedisValue[3]
    {
      (RedisValue) timeMilliseconds,
      (RedisValue) totalMilliseconds,
      (RedisValue) num
    }) == 1;
  }

  public async Task<bool> IsWithinQuotaAsync(
    ApiKeyMetadata metadata,
    CancellationToken cancellationToken)
  {
    if (metadata.DailyQuota <= 0)
      return true;
    IDatabase db = this._redis.GetDatabase();
    string key = $"quota:{metadata.Env}:{metadata.PartnerId}:{metadata.KeyId}:{DateTime.UtcNow:yyyyMMdd}";
    long current = await db.StringIncrementAsync((RedisKey) key);
    if (current == 1L)
    {
      int num = await db.KeyExpireAsync((RedisKey) key, new TimeSpan?(TimeSpan.FromHours(25))) ? 1 : 0;
    }
    return current <= (long) metadata.DailyQuota;
  }
}
