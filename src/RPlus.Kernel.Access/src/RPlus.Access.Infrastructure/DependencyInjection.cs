// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.DependencyInjection
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Application.Interfaces.Monitoring;
using RPlus.Access.Infrastructure.Clients;
using RPlus.Access.Infrastructure.Consumers;
using RPlus.Access.Infrastructure.Messaging.Consumers;
using RPlus.Access.Infrastructure.Messaging.Events;
using RPlus.Access.Infrastructure.Monitoring;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.Access.Infrastructure.Producers;
using RPlus.Access.Infrastructure.Services;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Integration;
using RPlus.SDK.Infrastructure.Services;
using System;
using System.Net.Http;

#nullable enable
namespace RPlus.Access.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    string connectionString = configuration.GetConnectionString("DefaultConnection");
    services.AddDbContext<AccessDbContext>((Action<DbContextOptionsBuilder>) (options => options.UseNpgsql(connectionString).ConfigureWarnings((Action<WarningsConfigurationBuilder>) (w => w.Ignore(RelationalEventId.PendingModelChangesWarning)))));
    services.AddScoped<IAccessDbContext>((Func<IServiceProvider, IAccessDbContext>) (provider => (IAccessDbContext) provider.GetRequiredService<AccessDbContext>()));
    // services.AddKernelRedis(configuration, "ConnectionStrings:Redis");
    services.AddStackExchangeRedisCache((Action<RedisCacheOptions>) (options => options.Configuration = configuration.GetConnectionString("Redis")));
    services.AddMemoryCache();
    services.AddSingleton<ISecretProvider, EnvSecretProvider>();
    services.AddSingleton<IIntegRateLimiter, IntegRateLimiter>();
    // services.AddKernelKafkaProducer(configuration);
    services.AddSingleton<IAccessMetrics, AccessMetrics>();
    services.AddHostedService<UserAssignmentConsumer>();
    services.AddSingleton<IAuditProducer, KafkaAuditProducer>();
    services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
    services.AddHttpClient<IOrganizationClient, HttpOrganizationClient>((Action<HttpClient>) (client => client.BaseAddress = new Uri("http://rplus-organization"))).AddStandardResilienceHandler();
    string kafkaBootstrap = configuration.GetSection("Kafka").Get<KafkaOptions>()?.BootstrapServers ?? configuration["Kafka__BootstrapServers"] ?? configuration.GetConnectionString("Kafka") ?? "kernel-kafka:9092";
    services.AddHostedService<UserCreatedConsumer>();
    services.AddHostedService<RootTableInitializer>();
    services.AddScoped<IKafkaConsumer<NodeMovedEvent>, OrgEventsConsumer>();
    services.AddSingleton<IHostedService>((Func<IServiceProvider, IHostedService>) (sp => (IHostedService) new KafkaConsumerService<NodeMovedEvent>(sp, kafkaBootstrap, "org.node.moved", "access-service-group")));
    services.AddHostedService<AuditCleanupWorker>();
    services.AddOpenTelemetry().ConfigureResource((Action<ResourceBuilder>) (resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? "rplus-kernel-access", serviceInstanceId: Environment.MachineName))).WithMetrics((Action<MeterProviderBuilder>) (metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())).WithTracing((Action<TracerProviderBuilder>) (tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()));
    return services;
  }
}
