// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Integration.IntegrationServiceCollectionExtensions
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;

#nullable enable
namespace RPlus.SDK.Infrastructure.Integration;

public static class IntegrationServiceCollectionExtensions
{
  public static IServiceCollection AddIntegrationRateLimiter(
    this IServiceCollection services,
    TimeSpan? window = null)
  {
    services.AddSingleton<IIntegRateLimiter>((Func<IServiceProvider, IIntegRateLimiter>) (sp => (IIntegRateLimiter) new IntegRateLimiter(sp.GetRequiredService<IConnectionMultiplexer>(), window)));
    return services;
  }

  public static IServiceCollection AddIntegrationGrpcProxy(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.Configure<IntegrationGrpcProxyOptions>((IConfiguration) configuration.GetSection("Integration:Grpc"));
    services.AddSingleton<IIntegrationGrpcProxy, IntegrationGrpcProxy>();
    return services;
  }
}
