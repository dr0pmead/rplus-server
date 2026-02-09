using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RPlus.SDK.Core.Messaging.Events;
using RPlus.SDK.Wallet.Events;
using RPlus.Wallet.Api.Grpc;
using RPlus.Wallet.Application;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;
using RPlus.Wallet.Infrastructure.Consumers;
using RPlus.Wallet.Infrastructure.Repositories;
using RPlus.Wallet.Infrastructure.Services;
using RPlus.Wallet.Persistence;
using StackExchange.Redis;
using Confluent.Kafka;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Services;
using RPlus.SDK.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("wallet");

// Configure Kestrel for HTTP/2 without TLS (h2c) - required for gRPC
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5005, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddWalletApplication();

builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? builder.Configuration["Redis"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConnection);
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
builder.Services.AddHostedService<WalletOutboxDispatcher>();

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

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "wallet";
    options.ApplicationId = "wallet";
    options.AccessGrpcAddress = builder.Configuration["Services:Access:Grpc"] ?? "http://rplus-kernel-access:5003";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedConsumer>();
    x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));

    x.AddRider(rider =>
    {
        rider.AddProducer<WalletTransactionCreated>(WalletEventTopics.TransactionCreated);
        rider.AddProducer<WalletBalanceChanged>(WalletEventTopics.BalanceChanged);
        rider.AddProducer<PromoAwarded>(WalletEventTopics.PromoAwarded);
        rider.AddProducer<WalletTransactionReversed>(WalletEventTopics.TransactionReversed);
        rider.AddProducer<WalletTransactionCommitted>(WalletEventTopics.TransactionCommitted);
        rider.AddProducer<WalletTransactionCancelled>(WalletEventTopics.TransactionCancelled);
        rider.AddConsumer<UserCreatedConsumer>();

        rider.UsingKafka((context, k) =>
        {
            k.Host(kafkaBootstrapServers);
            k.TopicEndpoint<UserCreated>("user.identity.v1", "rplus-wallet-service", e =>
            {
                e.ConfigureConsumer<UserCreatedConsumer>(context);
            });
        });
    });
});


var app = builder.Build();

await app.ApplyDatabaseMigrationsAsync(true, typeof(WalletDbContext));

app.MapGrpcService<WalletGrpcService>();
app.MapControllers();
app.MapGet("/", () => "Wallet gRPC service is running.");

app.Run();
