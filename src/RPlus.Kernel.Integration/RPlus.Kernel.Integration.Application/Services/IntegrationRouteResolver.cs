// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IntegrationRouteResolver
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RPlus.Kernel.Integration.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public class IntegrationRouteResolver : IIntegrationRouteResolver
{
  private readonly IIntegrationDbContext _db;
  private readonly IMemoryCache _cache;
  private const string CacheKey = "integration_routes";
  private readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60L);

  public IntegrationRouteResolver(IIntegrationDbContext db, IMemoryCache cache)
  {
    this._db = db;
    this._cache = cache;
  }

  public async Task<IntegrationRoute?> ResolveAsync(
    string endpoint,
    Guid? partnerId,
    CancellationToken cancellationToken)
  {
    List<IntegrationRoute> async = await this._cache.GetOrCreateAsync<List<IntegrationRoute>>((object) "integration_routes", (Func<ICacheEntry, Task<List<IntegrationRoute>>>) (async entry =>
    {
      entry.AbsoluteExpirationRelativeToNow = new TimeSpan?(this.CacheDuration);
      return await this._db.Routes.AsNoTracking<IntegrationRoute>().Where<IntegrationRoute>((Expression<Func<IntegrationRoute, bool>>) (r => r.IsActive)).OrderByDescending<IntegrationRoute, int>((Expression<Func<IntegrationRoute, int>>) (r => r.Priority)).ToListAsync<IntegrationRoute>(cancellationToken);
    }));
    if (async == null)
      return (IntegrationRoute) null;
    if (partnerId.HasValue)
    {
      IntegrationRoute match = IntegrationRouteResolver.FindMatch(async, endpoint, partnerId);
      if (match != null)
        return match;
    }
    return IntegrationRouteResolver.FindMatch(async, endpoint, new Guid?());
  }

  private static IntegrationRoute? FindMatch(
    List<IntegrationRoute> routes,
    string endpoint,
    Guid? partnerId)
  {
    foreach (IntegrationRoute match in routes.Where<IntegrationRoute>((Func<IntegrationRoute, bool>) (r =>
    {
      Guid? partnerId1 = r.PartnerId;
      Guid? nullable = partnerId;
      if (partnerId1.HasValue != nullable.HasValue)
        return false;
      return !partnerId1.HasValue || partnerId1.GetValueOrDefault() == nullable.GetValueOrDefault();
    })))
    {
      if (IntegrationRouteResolver.IsMatch(match.RoutePattern, endpoint))
        return match;
    }
    return (IntegrationRoute) null;
  }

  private static bool IsMatch(string pattern, string endpoint)
  {
    if (pattern == endpoint)
      return true;
    if (pattern.EndsWith("/**"))
    {
      string str1 = pattern;
      string str2 = str1.Substring(0, str1.Length - 3);
      return endpoint.StartsWith(str2, StringComparison.OrdinalIgnoreCase);
    }
    if (!pattern.EndsWith("/*"))
      return false;
    string str3 = pattern;
    string str4 = str3.Substring(0, str3.Length - 2);
    if (!endpoint.StartsWith(str4, StringComparison.OrdinalIgnoreCase))
      return false;
    string str5 = endpoint;
    int length = str4.Length;
    return !str5.Substring(length, str5.Length - length).TrimStart('/').Contains('/');
  }
}
