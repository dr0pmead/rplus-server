// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Infrastructure.Services.DbProxyConfig
// Assembly: RPlus.Gateway.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 54ABDD44-3C89-45DC-858E-4ECA8F349EB2
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Infrastructure.dll

using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Threading;
using Yarp.ReverseProxy.Configuration;

#nullable enable
namespace RPlus.Gateway.Infrastructure.Services;

public class DbProxyConfig : IProxyConfig
{
  private readonly CancellationTokenSource _cts = new CancellationTokenSource();

  public DbProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
  {
    this.Routes = routes;
    this.Clusters = clusters;
    this.ChangeToken = (IChangeToken) new CancellationChangeToken(this._cts.Token);
  }

  public IReadOnlyList<RouteConfig> Routes { get; }

  public IReadOnlyList<ClusterConfig> Clusters { get; }

  public IChangeToken ChangeToken { get; }

  internal void SignalChange() => this._cts.Cancel();
}
