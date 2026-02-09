// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Infrastructure.Services.GuardPolicyStore
// Assembly: RPlus.Kernel.Guard.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DF97D949-B080-4EE7-A993-4CFFBB255DD1
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Infrastructure.dll

using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Guard.Infrastructure.Services;

public sealed class GuardPolicyStore
{
  private const string MaintenanceKey = "sys:guard:maintenance";
  private const string MaintenanceReasonKey = "sys:guard:maintenance:reason";
  private const string IpBlockPrefix = "sys:guard:block:ip:";
  private const string RpsEnabledKey = "sys:guard:rps:enabled";
  private const string RpsLimitKey = "sys:guard:rps:limit";
  private const string RpsWindowKey = "sys:guard:rps:window";
  private readonly IConnectionMultiplexer _redis;

  public GuardPolicyStore(IConnectionMultiplexer redis) => this._redis = redis;

  public async Task<GuardMaintenanceStatus> GetMaintenanceAsync(CancellationToken cancellationToken)
  {
    var db = this._redis.GetDatabase();
    var enabled = await db.StringGetAsync(MaintenanceKey);
    var reason = await db.StringGetAsync(MaintenanceReasonKey);
    var ttl = await db.KeyTimeToLiveAsync(MaintenanceKey);
    return new GuardMaintenanceStatus(enabled == "1", reason.IsNullOrEmpty ? null : reason.ToString(), ttl);
  }

  public async Task SetMaintenanceAsync(
    bool enabled,
    string? reason,
    TimeSpan? ttl,
    CancellationToken cancellationToken)
  {
    var db = this._redis.GetDatabase();
    if (!enabled)
    {
      await db.KeyDeleteAsync(MaintenanceKey);
      await db.KeyDeleteAsync(MaintenanceReasonKey);
    }
    else
    {
      if (ttl.HasValue)
          await db.StringSetAsync(MaintenanceKey, "1", expiry: ttl.Value);
      else
          await db.StringSetAsync(MaintenanceKey, "1");

      if (!string.IsNullOrWhiteSpace(reason))
      {
          if (ttl.HasValue)
            await db.StringSetAsync(MaintenanceReasonKey, reason, expiry: ttl.Value);
          else
            await db.StringSetAsync(MaintenanceReasonKey, reason);
      }
      else
      {
        await db.KeyDeleteAsync(MaintenanceReasonKey);
      }
    }
  }

  public async Task BlockIpAsync(
    string ip,
    string? reason,
    TimeSpan? ttl,
    CancellationToken cancellationToken)
  {
    var db = this._redis.GetDatabase();
    string value = string.IsNullOrWhiteSpace(reason) ? "1" : reason;
    if (ttl.HasValue)
        await db.StringSetAsync($"{IpBlockPrefix}{ip}", value, expiry: ttl.Value);
    else
        await db.StringSetAsync($"{IpBlockPrefix}{ip}", value);
  }
  
  public Task UnblockIpAsync(string ip, CancellationToken cancellationToken)
  {
    return this._redis.GetDatabase().KeyDeleteAsync($"{IpBlockPrefix}{ip}");
  }

  public async Task<GuardIpBlockStatus> GetIpStatusAsync(
    string ip,
    CancellationToken cancellationToken)
  {
    var db = this._redis.GetDatabase();
    var value = await db.StringGetAsync($"{IpBlockPrefix}{ip}");
    if (value.IsNullOrEmpty)
      return new GuardIpBlockStatus(false, null, null);
    var ttl = await db.KeyTimeToLiveAsync($"{IpBlockPrefix}{ip}");
    return new GuardIpBlockStatus(true, value.ToString(), ttl);
  }

  public async Task<GuardRpsStatus> GetRpsAsync(CancellationToken cancellationToken)
  {
    var db = this._redis.GetDatabase();
    var enabledValue = await db.StringGetAsync(RpsEnabledKey);
    if (enabledValue.IsNullOrEmpty)
      return new GuardRpsStatus(false, 0, 0, null);
      
    var limitValue = await db.StringGetAsync(RpsLimitKey);
    var windowValue = await db.StringGetAsync(RpsWindowKey);
    var ttl = await db.KeyTimeToLiveAsync(RpsEnabledKey);
    
    int.TryParse(limitValue.ToString(), out int limit);
    int.TryParse(windowValue.ToString(), out int window);
    
    return new GuardRpsStatus(enabledValue == "1", limit, window, ttl);
  } 

  public async Task SetRpsAsync(
    bool enabled,
    int limit,
    int windowSeconds,
    TimeSpan? ttl,
    CancellationToken cancellationToken)
  {
    var db = this._redis.GetDatabase();
    if (!enabled)
    {
      await db.KeyDeleteAsync(RpsEnabledKey);
      await db.KeyDeleteAsync(RpsLimitKey);
      await db.KeyDeleteAsync(RpsWindowKey);
    }
    else
    {
       if (ttl.HasValue)
       {
          await db.StringSetAsync(RpsEnabledKey, "1", expiry: ttl.Value);
          await db.StringSetAsync(RpsLimitKey, limit.ToString(), expiry: ttl.Value);
          await db.StringSetAsync(RpsWindowKey, windowSeconds.ToString(), expiry: ttl.Value);
       }
       else
       {
          await db.StringSetAsync(RpsEnabledKey, "1");
          await db.StringSetAsync(RpsLimitKey, limit.ToString());
          await db.StringSetAsync(RpsWindowKey, windowSeconds.ToString());
       }
    }
  }
}
