// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Infrastructure.Services.DbProxyConfigProvider
// Assembly: RPlus.Gateway.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 54ABDD44-3C89-45DC-858E-4ECA8F349EB2
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RPlus.Gateway.Domain.Entities;
using RPlus.Gateway.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Yarp.ReverseProxy.Configuration;

#nullable enable
namespace RPlus.Gateway.Infrastructure.Services;

public class DbProxyConfigProvider : IProxyConfigProvider
{
  private readonly IServiceProvider _serviceProvider;
  private volatile DbProxyConfig _config;

  public DbProxyConfigProvider(IServiceProvider serviceProvider)
  {
    this._serviceProvider = serviceProvider;
    this._config = this.LoadConfig();
  }

  public IProxyConfig GetConfig() => (IProxyConfig) this._config;

  private DbProxyConfig LoadConfig()
  {
    using (IServiceScope scope = this._serviceProvider.CreateScope())
    {
      GatewayDbContext requiredService = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
      List<GatewayRoute> list1 = requiredService.Routes.Include<GatewayRoute, GatewayCluster>((Expression<Func<GatewayRoute, GatewayCluster>>) (r => r.Cluster)).Where<GatewayRoute>((Expression<Func<GatewayRoute, bool>>) (r => r.IsEnabled && !r.PathPattern.Contains("auth"))).AsNoTracking<GatewayRoute>().ToList<GatewayRoute>();
      List<GatewayCluster> list2 = requiredService.Clusters.AsNoTracking<GatewayCluster>().ToList<GatewayCluster>();
      return new DbProxyConfig((IReadOnlyList<RouteConfig>) list1.Select<GatewayRoute, RouteConfig>((Func<GatewayRoute, RouteConfig>) (r => new RouteConfig()
      {
        RouteId = r.RouteId,
        ClusterId = r.ClusterId,
        Match = new RouteMatch()
        {
          Path = r.PathPattern,
          Methods = ((IEnumerable<string>) r.Methods).Any<string>() ? (IReadOnlyList<string>) r.Methods : (IReadOnlyList<string>) (string[]) null
        },
        AuthorizationPolicy = r.AuthPolicy == "Anonymous" ? "Anonymous" : "Default",
        Metadata = (IReadOnlyDictionary<string, string>) new Dictionary<string, string>()
        {
          {
            "AccessPolicy",
            r.AccessPolicy ?? ""
          }
        }
      })).ToList<RouteConfig>(), (IReadOnlyList<ClusterConfig>) list2.Select<GatewayCluster, ClusterConfig>((Func<GatewayCluster, ClusterConfig>) (c => new ClusterConfig()
      {
        ClusterId = c.ClusterId,
        Destinations = (IReadOnlyDictionary<string, DestinationConfig>) new Dictionary<string, DestinationConfig>()
        {
          {
            "destination1",
            new DestinationConfig() { Address = c.Address }
          }
        },
        HealthCheck = (HealthCheckConfig) null
      })).ToList<ClusterConfig>());
    }
  }

  public void Reload()
  {
    DbProxyConfig config = this._config;
    this._config = this.LoadConfig();
    config.SignalChange();
  }
}
