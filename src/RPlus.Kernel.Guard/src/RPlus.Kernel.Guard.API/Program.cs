using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RPlus.Kernel.Guard.Domain.Services;
using RPlus.Kernel.Guard.Infrastructure.Services;
using RPlus.Kernel.Guard.Infrastructure.Persistence;
using RPlus.Kernel.Guard.Application.Pipelines;
using RPlus.Kernel.Guard.Api.Services;
using Confluent.Kafka;
using MassTransit;
using RPlus.SDK.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("guard");

// Kestrel: ensure HTTP/2 for gRPC (plain-text h2c)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5013, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddGrpc();

// Redis
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

// Postgres (Persistence)
builder.Services.AddDbContext<GuardDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Core Services
builder.Services.AddSingleton<IGuardStateStore, GuardRedisService>();
builder.Services.AddScoped<ISecurityPipeline, SecurityPipeline>();

// Outbox
builder.Services.AddHostedService<GuardOutboxDispatcher>();
builder.Services.AddSingleton<RPlus.SDK.Eventing.Abstractions.IEventPublisher, RPlus.SDK.Infrastructure.Services.KafkaEventPublisher>();

// MassTransit (Consumer)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RPlus.Kernel.Guard.Application.Consumers.SecuritySignalReceivedConsumer>();

    x.AddRider(rider =>
    {
        rider.UsingKafka((context, k) =>
        {
            k.Host(builder.Configuration["Kafka:BootstrapServers"] ?? "kernel-kafka:9092");

            k.TopicEndpoint<RPlus.SDK.Contracts.Domain.Security.SecuritySignalReceived_v1>("guard.signals.received", "guard-group", e =>
            {
                e.ConfigureConsumer<RPlus.Kernel.Guard.Application.Consumers.SecuritySignalReceivedConsumer>(context);
            });
        });
    });
});

// Kafka Producer for IEventPublisher
builder.Services.AddSingleton<Confluent.Kafka.IProducer<string, string>>(sp =>
{
    var config = new Confluent.Kafka.ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "kernel-kafka:9092"
    };
    return new Confluent.Kafka.ProducerBuilder<string, string>(config).Build();
});


var app = builder.Build();

// Ensure schema exists without relying on migrations snapshot
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GuardDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
app.MapControllers();
app.MapGrpcService<GuardGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
