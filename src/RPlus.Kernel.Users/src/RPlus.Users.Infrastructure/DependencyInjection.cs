// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.DependencyInjection
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RPlus.Core.Kafka;
using RPlus.SDK.Eventing;
using RPlus.Users.Application.Interfaces.Messaging;
using RPlus.Users.Application.Interfaces.Monitoring;
using RPlus.Users.Application.Interfaces.Repositories;
using RPlus.Users.Application.Interfaces.Services;
using RPlus.Users.Application.Options;
using RPlus.Users.Infrastructure.Consumers;
using RPlus.Users.Infrastructure.Messaging;
using RPlus.Users.Infrastructure.Monitoring;
using RPlus.Users.Infrastructure.Persistence;
using RPlus.Users.Infrastructure.Repositories;
using RPlus.Users.Infrastructure.Services;
using StackExchange.Redis;
using System;

#nullable enable
namespace RPlus.Users.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    string connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    
    services.AddDbContext<UsersDbContext>(options => 
        options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

    string redisConnString = configuration.GetValue<string>("Redis:ConnectionString") ?? "redis:6379,abortConnect=false";
    services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnString));

    services.AddHealthChecks()
        .AddNpgSql(connectionString)
        .AddRedis(redisConnString)
        .AddKafka(config => config.BootstrapServers = configuration.GetSection("Kafka").GetValue<string>("BootstrapServers") ?? "kafka:9092", name: "kafka");

    services.Configure<ProducerConfig>(configuration.GetSection("Kafka"));
    
    // Use SDK for Kafka producer registration if available, otherwise manual
    services.AddSingleton(typeof(IKafkaProducer<,>), typeof(KafkaProducer<,>));

    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IUserEventPublisher, UserEventPublisher>();
    services.AddSingleton<ICryptoService, CryptoService>();
    services.AddSingleton<IUserMetrics, UserMetrics>();
    services.AddSingleton<TimeProvider>(TimeProvider.System);
    services.AddSingleton<IUserQrService, UserQrService>();
    
    services.AddHostedService<UsersOutboxProcessor>();
    services.AddHostedService<UserCreatedConsumer>();
    services.AddHostedService<IntegrationScanContextConsumer>();

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? "rplus-kernel-users", serviceInstanceId: Environment.MachineName))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter())
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());

    return services;
  }
}
