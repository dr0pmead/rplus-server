// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Services.EnvSecretProvider
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using RPlus.Access.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Services;

public class EnvSecretProvider : ISecretProvider
{
  private readonly IConfiguration _configuration;
  private readonly IMemoryCache _cache;
  private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5L);

  public EnvSecretProvider(IConfiguration configuration, IMemoryCache cache)
  {
    this._configuration = configuration;
    this._cache = cache;
  }

  public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default (CancellationToken))
  {
    return this._cache.GetOrCreateAsync<string>((object) key, (Func<ICacheEntry, Task<string>>) (entry =>
    {
      entry.AbsoluteExpirationRelativeToNow = new TimeSpan?(this._cacheTtl);
      return Task.FromResult<string>(this._configuration[key]);
    }));
  }
}
