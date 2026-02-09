// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.DependencyInjection
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Confluent.Kafka;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using RPlus.Kernel.Integration.Infrastructure.Services;
using RPlus.SDK.Infrastructure.Integration;
using StackExchange.Redis;
using System;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddIntegrationInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    string connectionString = configuration.GetConnectionString("IntegrationDatabase") ?? "Host=audit-postgres;Database=integration;Username=audit;Password=audit";
    services.AddDbContext<IntegrationDbContext>((Action<DbContextOptionsBuilder>) (options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()));
    services.AddScoped<IIntegrationDbContext>((Func<IServiceProvider, IIntegrationDbContext>) (sp => (IIntegrationDbContext) sp.GetRequiredService<IntegrationDbContext>()));
    string redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:ConnectionString"] ?? "redis:6379";
    services.AddStackExchangeRedisCache((Action<RedisCacheOptions>) (options => options.Configuration = redisConnection));
    services.AddSingleton<IConnectionMultiplexer>((Func<IServiceProvider, IConnectionMultiplexer>) (_ => (IConnectionMultiplexer) ConnectionMultiplexer.Connect(redisConnection)));
    services.Configure<KafkaOptions>((IConfiguration) configuration.GetSection("Kafka"));
    services.AddOptions<ProducerConfig>()
      .Configure<IOptions<KafkaOptions>>((config, kafka) =>
      {
        config.BootstrapServers = kafka.Value.BootstrapServers;
        config.ClientId = "rplus-kernel-integration";
      });
    ServiceCollectionServiceExtensions.AddSingleton(services, typeof (IKafkaProducer<,>), typeof (KafkaProducer<,>));
    services.AddDataProtection().SetApplicationName("rplus-kernel-integration");
    string secretKey = configuration["Integration:SecretKey"] ?? configuration["INTEGRATION_SECRET_KEY"];
    if (string.IsNullOrWhiteSpace(secretKey))
    {
      services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    }
    else
    {
      // AES-GCM primary + DataProtection fallback for legacy keys
      services.AddSingleton<ISecretProtector>(sp =>
      {
        var primary = new AesSecretProtector(secretKey);
        var fallback = new DataProtectionSecretProtector(sp.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackSecretProtector>>();
        return new FallbackSecretProtector(primary, fallback, logger);
      });
    }
    services.AddSingleton<IApiKeyStore, ApiKeyStore>();
    services.AddSingleton<IIntegrationPartnerCache, IntegrationPartnerCache>();
    services.AddSingleton<IScanVisitStore, ScanVisitStore>();
    services.AddScoped<RPlus.Kernel.Integration.Infrastructure.Services.IntegrationAdminService>();
    services.AddScoped<IntegrationStatsQueryService>();
    services.AddIntegrationRateLimiter();
    services.AddIntegrationGrpcProxy(configuration);
    services.AddSingleton<IIntegrationStatsPublisher, IntegrationStatsPublisher>();
    services.AddHostedService<IntegrationStatsConsumer>();
    services.AddHostedService<IntegrationDbInitializer>();
    services.Configure<IntegrationStatsRetentionOptions>(configuration.GetSection("Integration:Stats:Retention"));
    services.AddHostedService<IntegrationStatsRetentionWorker>();
    services.AddOpenTelemetry().ConfigureResource((Action<ResourceBuilder>) (resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? "rplus-kernel-integration", serviceInstanceId: Environment.MachineName))).WithMetrics((Action<MeterProviderBuilder>) (metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())).WithTracing((Action<TracerProviderBuilder>) (tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()));

    // Proactive caching for <10ms scan responses
    services.AddSingleton<IScanProfileCache, ScanProfileCache>();
    // Note: IScanProfileAggregator is registered in API project (uses API-layer clients)
    services.AddHostedService<Consumers.LoyaltyProfileUpdatedConsumer>();
    services.AddHostedService<Consumers.HrProfileUpdatedConsumer>();

    return services;
  }
}
