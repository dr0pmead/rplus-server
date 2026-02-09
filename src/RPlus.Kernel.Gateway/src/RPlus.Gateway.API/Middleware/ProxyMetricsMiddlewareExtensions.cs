// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Api.Middleware.ProxyMetricsMiddlewareExtensions
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53C73046-40B0-469F-A259-3E029837F0C4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\ExecuteService.dll

using Microsoft.AspNetCore.Builder;

#nullable enable
namespace RPlus.Gateway.Api.Middleware;

public static class ProxyMetricsMiddlewareExtensions
{
  public static IApplicationBuilder UseProxyMetrics(this IApplicationBuilder builder)
  {
    return builder.UseMiddleware<ProxyMetricsMiddleware>();
  }
}


