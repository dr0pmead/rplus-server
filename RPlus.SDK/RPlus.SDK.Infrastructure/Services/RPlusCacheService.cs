// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Services.RPlusCacheService
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using Microsoft.Extensions.Caching.Distributed;
using RPlus.SDK.Core.Abstractions;
using System;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Infrastructure.Services;

public class RPlusCacheService : IRPlusCache
{
  private readonly IDistributedCache _cache;
  private readonly string _moduleId;
  private readonly ModuleCacheProfile _profile;

  public RPlusCacheService(IDistributedCache cache, IModuleManifest manifest)
  {
    this._cache = cache;
    this._moduleId = manifest.ModuleId;
    this._profile = manifest.Cache;
  }

  private string GetKey(string key) => $"rplus:{this._moduleId}:{key}";

  public async Task<T?> GetAsync<T>(string key)
  {
    if (!this._profile.Enabled)
      return default (T);
    string? stringAsync = await this._cache.GetStringAsync(this.GetKey(key));
    return stringAsync == null ? default (T) : JsonSerializer.Deserialize<T>(stringAsync);
  }

  public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
  {
    if (!this._profile.Enabled)
      return;
    DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
    {
      AbsoluteExpirationRelativeToNow = ttl ?? this._profile.DefaultTtl
    };
    string str = JsonSerializer.Serialize<T>(value);
    await this._cache.SetStringAsync(this.GetKey(key), str, options);
  }

  public async Task RemoveAsync(string key)
  {
    if (!this._profile.Enabled)
      return;
    await this._cache.RemoveAsync(this.GetKey(key));
  }
}
