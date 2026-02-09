// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.DependencyInjection.RPlusModuleExtensions
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RPlus.Core.Kafka;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Abstractions;
using RPlus.SDK.Infrastructure.Persistence;
using RPlus.SDK.Infrastructure.Pipeline;
using RPlus.SDK.Infrastructure.Services;
using System;
using System.Reflection;

#nullable enable
namespace RPlus.SDK.Infrastructure.DependencyInjection;

public static class RPlusModuleExtensions
{
  public static IServiceCollection AddRPlusModule<TManifest>(
    this IServiceCollection services,
    IConfiguration configuration,
    params Assembly[] additionalAssemblies)
    where TManifest : class, IModuleManifest, new()
  {
    TManifest manifest = new TManifest();
    return services.AddRPlusModule(configuration, (IModuleManifest) manifest, typeof (TManifest).Assembly, additionalAssemblies);
  }

  public static IServiceCollection AddRPlusModule(
    this IServiceCollection services,
    IConfiguration configuration,
    IModuleManifest manifest,
    Assembly manifestAssembly,
    params Assembly[] additionalAssemblies)
  {
    services.AddSingleton<IModuleManifest>(manifest);
    services.AddSingleton<IRPlusCache, RPlusCacheService>();
    services.AddSingleton<IRPlusMetrics, RPlusMetricsService>();
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg =>
    {
      cfg.RegisterServicesFromAssembly(manifestAssembly);
      if (additionalAssemblies != null && additionalAssemblies.Length != 0)
        cfg.RegisterServicesFromAssemblies(additionalAssemblies);
      cfg.AddOpenBehavior(typeof (LoggingBehavior<,>));
      cfg.AddOpenBehavior(typeof (ValidationBehavior<,>));
      cfg.AddOpenBehavior(typeof (SecurityContextBehavior<,>));
      cfg.AddOpenBehavior(typeof (PolicyBehavior<,>));
    }));
    services.AddSingleton<IFeatureFlags>(new StubFeatureFlags());

    if (manifest.Runtime.RequiresKafka)
    {
      string kafkaBootstrapServers = configuration["Kafka:BootstrapServers"]
        ?? configuration["Kafka__BootstrapServers"]
        ?? configuration.GetConnectionString("Kafka")
        ?? string.Empty;
      if (string.IsNullOrWhiteSpace(kafkaBootstrapServers))
        throw new InvalidOperationException("Kafka bootstrap servers are not configured.");
      
       // Kafka Producer registration removed or simplified if generic required
      services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
       services.AddMassTransit((Action<IBusRegistrationConfigurator>) (x =>
      {
        x.SetKebabCaseEndpointNameFormatter();
        x.UsingInMemory((Action<IBusRegistrationContext, IInMemoryBusFactoryConfigurator>) ((context, cfg) => cfg.ConfigureEndpoints<IInMemoryReceiveEndpointConfigurator>(context)));
        x.AddRider((Action<IRiderRegistrationConfigurator>) (rider =>
        {
          // ModuleRegisteredEvent removed
          rider.UsingKafka((Action<IRiderRegistrationContext, IKafkaFactoryConfigurator>) ((context, k) => k.Host(kafkaBootstrapServers)));
        }));
      }));
    }
    if (manifest.Runtime.RequiresRedis)
    {
      string redisConnectionString = configuration["Redis:ConnectionString"]
        ?? configuration.GetConnectionString("Redis")
        ?? string.Empty;
      if (string.IsNullOrWhiteSpace(redisConnectionString))
        throw new InvalidOperationException("Redis connection string is not configured.");
      services.AddStackExchangeRedisCache((Action<RedisCacheOptions>) (options =>
      {
        options.Configuration = redisConnectionString;
        options.InstanceName = $"rplus:{manifest.ModuleId}:";
      }));
    }
    // Simplified health checks
    // Simplified health checks
    /*
    IHealthChecksBuilder builder = services.AddHealthChecks();
    if (manifest.Runtime.RequiresRedis)
    {
      string redisConnectionString = configuration["Redis:ConnectionString"] ?? configuration.GetConnectionString("Redis");
      if (!string.IsNullOrWhiteSpace(redisConnectionString))
        builder.AddRedis(redisConnectionString, "redis");
    }
    if (manifest.Runtime.RequiresDatabase)
      builder.AddDbContextCheck<RPlusDbContext>("db");
    */
    // Basic health check
    services.AddHealthChecks();
    if (manifest is IModuleStartup moduleStartup)
      moduleStartup.ConfigureServices(services, configuration);
    return services;
  }



  public static IServiceCollection AddRPlusDbContext<TContext>(
    this IServiceCollection services,
    Action<DbContextOptionsBuilder>? optionsAction = null)
    where TContext : DbContext
  {
    services.AddDbContext<TContext>(optionsAction);
    return services;
  }
}
