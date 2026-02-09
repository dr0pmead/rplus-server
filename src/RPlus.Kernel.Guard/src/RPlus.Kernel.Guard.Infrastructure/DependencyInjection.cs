// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Infrastructure.DependencyInjection
// Assembly: RPlus.Kernel.Guard.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DF97D949-B080-4EE7-A993-4CFFBB255DD1
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Infrastructure.dll

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RPlus.Kernel.Guard.Application.Services;
using RPlus.Kernel.Guard.Infrastructure.Services;
using RPlus.Core.Kafka;
using StackExchange.Redis;
using Confluent.Kafka;
using System;

#nullable enable
namespace RPlus.Kernel.Guard.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddGuardInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    // Services
    string redisConnString = configuration.GetValue<string>("Redis:ConnectionString") ?? "redis:6379,abortConnect=false";
    services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnString));
    
    services.AddHealthChecks().AddRedis(redisConnString);

    services.Configure<Confluent.Kafka.ProducerConfig>(configuration.GetSection("Kafka"));
    services.AddSingleton(typeof(RPlus.Core.Kafka.IKafkaProducer<,>), typeof(RPlus.Core.Kafka.KafkaProducer<,>));

    services.AddMemoryCache();
    services.AddSingleton<IPowService, PowService>();
    services.AddOpenTelemetry().ConfigureResource((Action<ResourceBuilder>) (resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? "rplus-kernel-guard", serviceInstanceId: Environment.MachineName))).WithMetrics((Action<MeterProviderBuilder>) (metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())).WithTracing((Action<TracerProviderBuilder>) (tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()));
    return services;
  }
}
