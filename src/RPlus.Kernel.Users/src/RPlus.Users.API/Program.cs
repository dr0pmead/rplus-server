using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using RPlus.SDK.Infrastructure.Services;
using RPlus.Users.Api.Services;
using RPlus.Users.Api.Schema;
using RPlus.Users.Application;
using RPlus.Users.Application.Options;
using RPlus.Users.Infrastructure;
using RPlus.Users.Infrastructure.Persistence;
using RPlus.SDK.Infrastructure.Extensions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("users");

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// v2: publish event schemas to Kafka (source) + Redis (cache)
builder.Services.AddSingleton<RPlus.SDK.Eventing.SchemaRegistry.IEventSchemaSource, UsersEventSchemaSource>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // Split ports to avoid Kestrel disabling HTTP/2 when HTTP/1.1 is also enabled without TLS.
    // - 5014: HTTP/1.1 (health checks, future controllers)
    // - 5015: HTTP/2 (gRPC, h2c inside docker network)
    options.ListenAnyIP(5014, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5015, listen => listen.Protocols = HttpProtocols.Http2);
});

var redisConnectionString =
    builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["ConnectionStrings:Redis"]
    ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));

var kafkaBootstrapServers = builder.Configuration.GetConnectionString("Kafka")
                            ?? builder.Configuration["Kafka:BootstrapServers"]
                            ?? builder.Configuration["Kafka"]
                            ?? "kernel-kafka:9092";
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers
    };

    return new ProducerBuilder<string, string>(config).Build();
});
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddEventSchemaRegistryPublisher(builder.Configuration);
builder.Services.Configure<UserQrOptions>(builder.Configuration.GetSection("Users:Qr"));

var keyCache = new JwtKeyCache();
builder.Services.AddSingleton(keyCache);
builder.Services.AddHostedService<JwtKeyFetchService>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKernelServiceDefaults(builder.Configuration);

builder.Services.Configure<UsersMetaClientOptions>(builder.Configuration.GetSection("Users:Meta"));
builder.Services.AddGrpcClient<RPlusGrpc.Meta.MetaService.MetaServiceClient>(o =>
{
    var metaGrpcAddress =
        builder.Configuration["Users:Meta:GrpcAddress"]
        ?? builder.Configuration["Services:Meta:Grpc"]
        ?? $"http://{builder.Configuration["META_GRPC_HOST"] ?? "rplus-kernel-meta"}:{builder.Configuration["META_GRPC_PORT"] ?? "5019"}";
    o.Address = new Uri(metaGrpcAddress);
});

builder.Services.AddGrpc();
builder.Services.AddHostedService<UsersPermissionManifestPublisherHostedService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var configuration = builder.Configuration;
        options.RequireHttpsMetadata = false;

        var issuer = configuration["JWT:Issuer"]
                    ?? configuration["JWT__ISSUER"]
                    ?? "RPlus.Auth";

        var issuerVariants = new[]
        {
            issuer,
            issuer.TrimEnd('/'),
            issuer.EndsWith("/") ? issuer : $"{issuer}/"
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = issuerVariants,
            ValidateAudience = true,
            ValidAudience = configuration["JWT:Audience"] ?? configuration["JWT__AUDIENCE"] ?? "RPlus.Kernel",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };

        options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) => keyCache.GetKeys();
    });
builder.Services.AddAuthorization();


var app = builder.Build();

app.UseKernelServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<UsersGrpcService>();
app.MapHealthChecks("/health");
app.MapControllers();

// Apply database migrations (including FIO removal migration)
await app.ApplyDatabaseMigrationsAsync(throwOnFailure: true, typeof(UsersDbContext));

app.Run();
