// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.DependencyInjection
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ...
// Assembly location: ...

using Fido2NetLib;
using Confluent.Kafka;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using EFCore.NamingConventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.Auth.Infrastructure.Monitoring;
using RPlus.Auth.Infrastructure.Services;
using RPlus.Core.Kafka;
using RPlusGrpc.Guard;
using StackExchange.Redis;
using System;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Services;
using System.IO;

#nullable enable
namespace RPlus.Auth.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var kafkaBootstrapServers =
      configuration.GetConnectionString("Kafka")
      ?? configuration["Kafka:BootstrapServers"]
      ?? configuration["Kafka"]
      ?? "kernel-kafka:9092";

    services.AddDbContext<AuthDbContext>((Action<DbContextOptionsBuilder>) (options => options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")).UseSnakeCaseNamingConvention()));
    services.AddScoped<IAuthDataService, AuthDataService>();
    services.AddScoped<IOutboxRepository, OutboxRepository>();
    services.AddScoped<IUserAuthEventPublisher, UserAuthEventPublisher>();
    services.AddScoped<IProtectionService, GuardPowProtectionService>();
    services.AddSingleton<ICryptoService, CryptoService>();

    // Infrastructure time source (used by token issuance/expiry calculations).
    // Registered explicitly so services can depend on TimeProvider and be testable.
    services.AddSingleton<TimeProvider>((Func<IServiceProvider, TimeProvider>) (_ => TimeProvider.System));

    services.AddSingleton<ITokenService, TokenService>();
    services.AddSingleton<IPhoneUtil, PhoneUtil>();

    // OTP + anti-abuse rate limiting
    services.AddMemoryCache();
    services.AddSingleton<IRedisRateLimitService, ResilientRateLimitService>();
    services.AddSingleton<ISecurityMetrics, SecurityMetrics>();

    services.AddScoped<IOtpService, OtpService>();
    services.AddTransient<IOtpDeliveryService, SmsOtpDeliveryService>();

    // Short Code for partner scan fallback (OTP alternative when QR unavailable)
    services.AddSingleton<IShortCodeService, ShortCodeService>();

    // JWT signing keys (required by TokenService + gRPC key export)
    // Backed by Redis; private key material is protected via ASP.NET DataProtection.
    var dataProtectionBuilder = services.AddDataProtection();
    var keyRingPath =
      configuration["DataProtection:KeyRingPath"]
      ?? configuration["AUTH_DATA_PROTECTION_PATH"];
    if (!string.IsNullOrWhiteSpace(keyRingPath))
    {
      Directory.CreateDirectory(keyRingPath);
      dataProtectionBuilder
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName("rplus-auth");
    }

    string redisConnectionString =
      configuration["Redis:ConnectionString"]
      ?? configuration.GetConnectionString("Redis")
      ?? configuration["ConnectionStrings:Redis"]
      ?? "localhost:6379,abortConnect=false";

    services.AddSingleton<IConnectionMultiplexer>((Func<IServiceProvider, IConnectionMultiplexer>) (_ => ConnectionMultiplexer.Connect(redisConnectionString)));
    services.AddSingleton<IJwtKeyStore, JwtKeyStore>();
    services.AddSingleton<IJwtKeyProvider, JwtKeyProvider>();
    services.AddHostedService<JwtKeyRotationService>();

    // System admin wizard helpers (Redis-backed temp flows + TOTP + encrypted secrets)
    services.AddSingleton<ISystemAuthFlowStore, SystemAuthFlowStore>();
    services.AddSingleton<IVaultCryptoService, VaultCryptoService>();
    services.AddSingleton<ITotpService, TotpService>();
    
    // Fido2
    services.AddFido2((Action<Fido2Configuration>) (options =>
    {
      options.ServerDomain = configuration["Fido2:ServerDomain"];
      options.ServerName = configuration["Fido2:ServerName"];
      options.Origins = configuration.GetSection("Fido2:Origins").Get<System.Collections.Generic.HashSet<string>>();
      options.TimestampDriftTolerance = configuration.GetValue<int>("Fido2:TimestampDriftTolerance", 300000);
    }));

    // gRPC Client
    services.AddGrpcClient<GuardService.GuardServiceClient>((Action<Grpc.Net.ClientFactory.GrpcClientFactoryOptions>) (o => o.Address = new Uri(configuration["Services:Guard"] ?? "http://localhost:5004")));

    // Eventing (Kafka)
    // - IEventPublisher is used by LoginWithPasswordCommandHandler to publish audit/security events.
    // - AuthOutboxProcessor publishes outbox messages (UserCreated/UserAuthUpdated/etc) to Kafka reliably.
    services.Configure<ProducerConfig>(opts => opts.BootstrapServers = kafkaBootstrapServers);
    services.AddSingleton<IKafkaProducer<string, string>, KafkaProducer<string, string>>();
    services.AddHostedService<AuthOutboxProcessor>();

    services.AddSingleton<IProducer<string, string>>(sp =>
    {
      var config = sp.GetRequiredService<IOptions<ProducerConfig>>().Value;
      return new ProducerBuilder<string, string>(config).Build();
    });

    services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

    // System admin lifecycle (idempotent) + schema upgrades for EnsureCreated-based Auth DB.
    services.AddHttpClient("access-root-bootstrap");
    services.AddHostedService<AuthSchemaUpgradeHostedService>();
    services.AddHostedService<SystemUserSeeder>();

    return services;
  }
}
