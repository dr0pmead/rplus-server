// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Infrastructure.DependencyInjection
// Assembly: RPlus.Gateway.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 54ABDD44-3C89-45DC-858E-4ECA8F349EB2
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RPlus.Gateway.Application.Interfaces.Observability;
using RPlus.Gateway.Infrastructure.Observability;
using RPlus.Gateway.Persistence;
using RPlus.Gateway.Infrastructure.Services;
using System;
using Yarp.ReverseProxy.Configuration;

#nullable enable
namespace RPlus.Gateway.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddDbContext<GatewayDbContext>((Action<DbContextOptionsBuilder>) (options => options.UseNpgsql(configuration.GetConnectionString("Default")).ConfigureWarnings((Action<WarningsConfigurationBuilder>) (w => w.Ignore(RelationalEventId.PendingModelChangesWarning)))));
    services.AddScoped<IGatewayDbContext>(sp => sp.GetRequiredService<GatewayDbContext>());
    services.AddStackExchangeRedisCache((Action<RedisCacheOptions>) (options => options.Configuration = configuration["Redis:ConnectionString"]));
    services.AddSingleton<IProxyConfigProvider, DbProxyConfigProvider>();
    services.AddScoped<RedisContextCache>();
    services.AddSingleton<IGatewayMetrics, GatewayMetrics>();
    services.AddOpenTelemetry().ConfigureResource((Action<ResourceBuilder>) (resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? "rplus-kernel-gateway", serviceInstanceId: Environment.MachineName))).WithMetrics((Action<MeterProviderBuilder>) (metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())).WithTracing((Action<TracerProviderBuilder>) (tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()));
    return services;
  }
}
