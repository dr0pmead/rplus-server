// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Infrastructure.DependencyInjection
// Assembly: RPlus.Kernel.Audit.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 271DD6D6-68D7-47FD-8F9A-65D4B328CF02
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Infrastructure.dll

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Infrastructure.Messaging;
using RPlus.Kernel.Audit.Infrastructure.Persistence;
using RPlus.Kernel.Audit.Infrastructure.Repositories;
using RPlus.Kernel.Audit.Infrastructure.Services;
using RPlus.Kernel.Infrastructure.Extensions;
using System;

#nullable enable
namespace RPlus.Kernel.Audit.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddAuditInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    string connectionString = configuration.GetConnectionString("AuditDatabase") ?? "Host=audit-postgres;Database=audit;Username=audit;Password=audit";
    NpgsqlDataSourceBuilder npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    npgsqlDataSourceBuilder.EnableDynamicJson();
    NpgsqlDataSource dataSource = npgsqlDataSourceBuilder.Build();
    services.AddDbContext<AuditDbContext>((Action<DbContextOptionsBuilder>) (options => options.UseNpgsql(dataSource)));
    string kafkaBootstrapServers = configuration.GetSection("Kafka")["BootstrapServers"] ?? configuration.GetConnectionString("Kafka") ?? "kafka:9092";
    services.AddSingleton(new ProducerConfig
    {
      BootstrapServers = kafkaBootstrapServers,
      ClientId = "rplus-kernel-audit"
    });
    services.AddScoped<IAuditRepository, AuditRepository>();
    services.AddSingleton<IAuditPublisher, KafkaAuditPublisher>();
    services.AddHostedService<AuditEventConsumer>();
    services.Configure<AuditRetentionOptions>(configuration.GetSection("Audit:Retention"));
    services.AddHostedService<AuditRetentionWorker>();
    services.AddOpenTelemetry().ConfigureResource((Action<ResourceBuilder>) (resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? "rplus-kernel-audit", serviceInstanceId: Environment.MachineName))).WithMetrics((Action<MeterProviderBuilder>) (metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())).WithTracing((Action<TracerProviderBuilder>) (tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()));
    return services;
  }
}
