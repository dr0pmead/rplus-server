// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.ServiceRegistryService
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using StackExchange.Redis;
using System;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public class ServiceRegistryService : IServiceRegistry
{
  private readonly IServiceProvider _serviceProvider;
  private readonly IConnectionMultiplexer _redis;
  private readonly ILogger<ServiceRegistryService> _logger;
  private const string CacheKeyPrefix = "service_registry:";

  public ServiceRegistryService(
    IServiceProvider serviceProvider,
    IConnectionMultiplexer redis,
    ILogger<ServiceRegistryService> logger)
  {
    this._serviceProvider = serviceProvider;
    this._redis = redis;
    this._logger = logger;
  }

  public async Task<ServiceRegistryEntry?> GetServiceAsync(string serviceName, CancellationToken ct = default (CancellationToken))
  {
    IDatabase dbRed = this._redis.GetDatabase();
    string cacheKey = "service_registry:" + serviceName.ToLowerInvariant();
    RedisValue async = await dbRed.StringGetAsync((RedisKey) cacheKey);
    if (!async.IsNull)
    {
      try
      {
        return JsonSerializer.Deserialize<ServiceRegistryEntry>(async.ToString());
      }
      catch
      {
      }
    }
    using (IServiceScope scope = this._serviceProvider.CreateScope())
    {
      ServiceRegistryEntry entry = await scope.ServiceProvider.GetRequiredService<IAccessDbContext>().ServiceRegistry.AsNoTracking<ServiceRegistryEntry>().FirstOrDefaultAsync<ServiceRegistryEntry>((Expression<Func<ServiceRegistryEntry, bool>>) (s => s.ServiceName == serviceName), ct);
      if (entry != null)
      {
        int num = await dbRed.StringSetAsync((RedisKey) cacheKey, (RedisValue) JsonSerializer.Serialize<ServiceRegistryEntry>(entry), (Expiration) TimeSpan.FromMinutes(5L)) ? 1 : 0;
      }
      return entry;
    }
  }

  public async Task RegisterOrUpdateAsync(
    string serviceName,
    string baseUrl,
    string publicKeys,
    ServiceCriticality criticality,
    CancellationToken ct = default (CancellationToken))
  {
    IAccessDbContext dbContext;
    using (IServiceScope scope = this._serviceProvider.CreateScope())
    {
      dbContext = scope.ServiceProvider.GetRequiredService<IAccessDbContext>();
      ServiceRegistryEntry serviceRegistryEntry = await dbContext.ServiceRegistry.FirstOrDefaultAsync<ServiceRegistryEntry>((Expression<Func<ServiceRegistryEntry, bool>>) (s => s.ServiceName == serviceName), ct);
      if (serviceRegistryEntry == null)
      {
        dbContext.ServiceRegistry.Add(new ServiceRegistryEntry()
        {
          ServiceName = serviceName,
          BaseUrl = baseUrl,
          PublicKeys = publicKeys,
          Criticality = criticality,
          LastSeen = DateTime.UtcNow
        });
      }
      else
      {
        serviceRegistryEntry.BaseUrl = baseUrl;
        serviceRegistryEntry.PublicKeys = publicKeys;
        serviceRegistryEntry.Criticality = criticality;
        serviceRegistryEntry.LastSeen = DateTime.UtcNow;
      }
      int num1 = await dbContext.SaveChangesAsync(ct);
      int num2 = await this._redis.GetDatabase().KeyDeleteAsync((RedisKey) ("service_registry:" + serviceName.ToLowerInvariant())) ? 1 : 0;
    }
    dbContext = (IAccessDbContext) null;
  }

  public async Task RefreshCacheAsync(CancellationToken ct = default (CancellationToken))
  {
  }
}
