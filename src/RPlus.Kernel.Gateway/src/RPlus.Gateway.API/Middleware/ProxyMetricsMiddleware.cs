// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Api.Middleware.ProxyMetricsMiddleware
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53C73046-40B0-469F-A259-3E029837F0C4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\ExecuteService.dll

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RPlus.Gateway.Application.Interfaces.Observability;
using System.Diagnostics;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Model;

#nullable enable
namespace RPlus.Gateway.Api.Middleware;

public class ProxyMetricsMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IGatewayMetrics _metrics;
  private readonly ILogger<ProxyMetricsMiddleware> _logger;

  public ProxyMetricsMiddleware(
    RequestDelegate next,
    IGatewayMetrics metrics,
    ILogger<ProxyMetricsMiddleware> logger)
  {
    this._next = next;
    this._metrics = metrics;
    this._logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    this._logger.LogInformation("ProxyMetrics: Middleware invoked for {Path}", (object) context.Request.Path);
    long start = Stopwatch.GetTimestamp();
    await this._next(context);
    IReverseProxyFeature reverseProxyFeature = context.GetReverseProxyFeature();
    if (reverseProxyFeature != null)
    {
      double totalSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
      string clusterId = reverseProxyFeature.Cluster.Config.ClusterId;
      int statusCode = context.Response.StatusCode;
      this._logger.LogInformation("ProxyMetrics: Recording request for cluster {Cluster}, Status {Status}", (object) clusterId, (object) statusCode);
      this._metrics.RecordProxyRequest(clusterId, statusCode, totalSeconds);
    }
    else
      this._logger.LogWarning("ProxyMetrics: No ProxyFeature found for {Path}", (object) context.Request.Path);
  }
}


