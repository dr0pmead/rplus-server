using Confluent.Kafka;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RPlus.Core.Options;
using RPlus.Loyalty.Application.Handlers;
using RPlus.Loyalty.Infrastructure.Consumers;
using RPlus.Loyalty.Infrastructure.Services;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Services;
using RPlus.SDK.Loyalty.Abstractions;
using RPlus.SDK.Infrastructure.Extensions;
using RPlus.Loyalty.Infrastructure.Options;
using Microsoft.Extensions.Options;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using StackExchange.Redis;
using RPlus.Loyalty.Application.Abstractions;
using RPlusGrpc.Users;
using RPlusGrpc.Hr;
using RPlusGrpc.Meta;
using RPlusGrpc.Runtime;
using RPlus.SDK.Eventing.SchemaRegistry;
using RPlus.Loyalty.Api.Schema;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;
using RPlus.Loyalty.Api.Options;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("loyalty");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // 5012: HTTP/1.1 (controllers, Swagger)
    // 5013: HTTP/2 (gRPC, h2c inside docker network)
    options.ListenAnyIP(5012, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(5013, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "loyalty";
    options.ApplicationId = "loyalty";
    options.AccessGrpcAddress = builder.Configuration["Services:Access:Grpc"] ?? "http://rplus-kernel-access:5003";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});

var loyaltyConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? builder.Configuration["Loyalty:ConnectionString"]
                           ?? "Host=localhost;Database=rplus_loyalty;Username=postgres;Password=postgres";

builder.Services.AddDbContext<LoyaltyDbContext>(options =>
{
    options.UseNpgsql(loyaltyConnectionString);
});
builder.Services.AddDbContextFactory<LoyaltyDbContext>(options => options.UseNpgsql(loyaltyConnectionString));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProcessLoyaltyEventCommandHandler).Assembly));
builder.Services.AddSingleton<RPlus.Loyalty.Application.Graph.LoyaltyGraphNodeCatalog>();
builder.Services.AddSingleton<RPlus.Loyalty.Application.Graph.ILoyaltyGraphNodeCatalog, MetaGraphNodeCatalog>();
builder.Services.AddSingleton<RPlus.Loyalty.Application.Graph.LoyaltyGraphSchemaValidator>();
builder.Services.AddSingleton<ILoyaltyLevelCatalog, MetaLoyaltyLevelCatalog>();
builder.Services.AddSingleton<ITenureLevelRecalculator, TenureLevelRecalculator>();
builder.Services.AddSingleton<IMotivationalTierCatalog, MetaMotivationalTierCatalog>();
builder.Services.AddSingleton<IMotivationalTierRecalculator, MotivationalTierRecalculator>();
builder.Services.AddMemoryCache();

// Delayed tenure recalculation - runs 5 min after startup to allow all services to start
builder.Services.AddHostedService<RPlus.Loyalty.Api.Services.DelayedTenureRecalculationService>();

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<LoyaltyIngressOptions>(builder.Configuration.GetSection("Loyalty:Ingress"));
builder.Services.Configure<LoyaltyDynamicConsumptionOptions>(builder.Configuration.GetSection(LoyaltyDynamicConsumptionOptions.SectionName));
builder.Services.Configure<LoyaltyUserContextOptions>(builder.Configuration.GetSection(LoyaltyUserContextOptions.SectionName));
builder.Services.Configure<LoyaltySchedulerOptions>(builder.Configuration.GetSection(LoyaltySchedulerOptions.SectionName));
builder.Services.Configure<LoyaltyCronOptions>(builder.Configuration.GetSection(LoyaltyCronOptions.SectionName));
builder.Services.Configure<LoyaltyIngressTestOptions>(builder.Configuration.GetSection(LoyaltyIngressTestOptions.SectionName));
builder.Services.Configure<LoyaltyMetaOptions>(builder.Configuration.GetSection(LoyaltyMetaOptions.SectionName));
builder.Services.Configure<LoyaltyRuntimeOptions>(builder.Configuration.GetSection(LoyaltyRuntimeOptions.SectionName));

// v2 registry (Redis cache reader)
var redisConnectionString =
    builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["ConnectionStrings:Redis"]
    ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IEventSchemaSource, LoyaltyEventSchemaSource>();
builder.Services.AddEventSchemaRegistryPublisher(builder.Configuration);

var usersGrpcAddress =
    builder.Configuration[$"{LoyaltyUserContextOptions.SectionName}:UsersGrpcAddress"]
    ?? builder.Configuration["Services:UsersGrpc"]
    ?? "http://rplus-kernel-users:5014";
builder.Services.AddGrpcClient<UsersService.UsersServiceClient>(o => o.Address = new Uri(usersGrpcAddress));

var hrGrpcAddress =
    builder.Configuration[$"{LoyaltyUserContextOptions.SectionName}:HrGrpcAddress"]
    ?? builder.Configuration["Services:HrGrpc"]
    ?? "http://rplus-kernel-hr:5016";
builder.Services.AddGrpcClient<HrService.HrServiceClient>(o => o.Address = new Uri(hrGrpcAddress));

var orgBase =
    builder.Configuration[$"{LoyaltyUserContextOptions.SectionName}:OrganizationBaseAddress"]
    ?? builder.Configuration["Services:Organization"]
    ?? "http://rplus-kernel-organization:5009/";
builder.Services.AddHttpClient("Organization", client =>
{
    client.BaseAddress = new Uri(orgBase, UriKind.Absolute);
});

var metaGrpcAddress =
    builder.Configuration[$"{LoyaltyMetaOptions.SectionName}:GrpcAddress"]
    ?? builder.Configuration["Services:Meta:Grpc"]
    ?? "http://rplus-kernel-meta:5019";
builder.Services.AddGrpcClient<MetaService.MetaServiceClient>(o => o.Address = new Uri(metaGrpcAddress));
builder.Services.AddSingleton<IUserContextProvider, UsersGrpcUserContextProvider>();

var runtimeGrpcAddress =
    builder.Configuration[$"{LoyaltyRuntimeOptions.SectionName}:GrpcAddress"]
    ?? builder.Configuration["Services:Runtime:Grpc"]
    ?? "http://rplus-kernel-runtime:5021";
builder.Services.AddGrpcClient<RuntimeService.RuntimeServiceClient>(o => o.Address = new Uri(runtimeGrpcAddress));
builder.Services.AddSingleton<IRuntimeGraphClient, RuntimeGrpcGraphClient>();

// Access gRPC for isRoot check
var accessGrpcAddress =
    builder.Configuration["Services:Access:Grpc"]
    ?? "http://rplus-kernel-access:5003";
builder.Services.AddGrpcClient<RPlusGrpc.Access.AccessService.AccessServiceClient>(o => o.Address = new Uri(accessGrpcAddress));

// Wallet gRPC for test accrual
var walletGrpcAddress =
    builder.Configuration["Services:Wallet:Grpc"]
    ?? "http://rplus-kernel-wallet:5005";
builder.Services.AddGrpcClient<RPlusGrpc.Wallet.WalletService.WalletServiceClient>(o => o.Address = new Uri(walletGrpcAddress));


var kafkaBootstrapServers = builder.Configuration.GetConnectionString("Kafka")
                            ?? builder.Configuration["Kafka:BootstrapServers"]
                            ?? builder.Configuration["Kafka"]
                            ?? "kernel-kafka:9092";

var kafkaGroupId = builder.Configuration["Kafka:GroupId"] ?? "rplus-loyalty-engine";

builder.Services.PostConfigure<KafkaOptions>(options =>
{
    options.BootstrapServers = string.IsNullOrWhiteSpace(options.BootstrapServers)
        ? kafkaBootstrapServers
        : options.BootstrapServers;
    options.GroupId = string.IsNullOrWhiteSpace(options.GroupId) ? kafkaGroupId : options.GroupId;
});

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers
    };

    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<ILoyaltyRuleEvaluator, BasicLoyaltyRuleEvaluator>();
builder.Services.AddHostedService<LoyaltyOutboxDispatcher>();
builder.Services.AddHostedService<LoyaltyTriggerConsumer>();
builder.Services.AddHostedService<LoyaltySchedulerHostedService>();
builder.Services.AddHostedService<LoyaltyCronHostedService>();
builder.Services.AddHostedService<UserCreatedConsumer>();

// Leaderboard system
builder.Services.AddSingleton<ILeaderboardService, RedisLeaderboardService>();
builder.Services.AddSingleton<RPlus.Loyalty.Infrastructure.Jobs.ILeaderboardRewardCatalog, MetaLeaderboardRewardCatalog>();
builder.Services.AddScoped<RPlus.Loyalty.Infrastructure.Services.IRewardDistributor, RewardDistributor>();
builder.Services.AddHostedService<LeaderboardActivityConsumer>();
builder.Services.AddSingleton<RPlus.Loyalty.Infrastructure.Jobs.MonthlyLeaderboardSnapshotJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RPlus.Loyalty.Infrastructure.Jobs.MonthlyLeaderboardSnapshotJob>());

// Wallet balance cache with Kafka sync
builder.Services.AddSingleton<RPlus.Loyalty.Infrastructure.Services.IWalletBalanceCache, RPlus.Loyalty.Infrastructure.Services.WalletBalanceCache>();
builder.Services.AddHostedService<RPlus.Loyalty.Infrastructure.Consumers.WalletBalanceUpdatedConsumer>();

var dynamicIngressEnabled = builder.Configuration.GetValue<bool>($"{LoyaltyDynamicConsumptionOptions.SectionName}:Enabled");
if (dynamicIngressEnabled)
{
    builder.Services.AddHostedService<LoyaltyDynamicIngressConsumer>();
}
else
{
    var ingress = builder.Configuration.GetSection("Loyalty:Ingress").Get<LoyaltyIngressOptions>() ?? new LoyaltyIngressOptions();
    foreach (var topic in ingress.Topics.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(topic))
            continue;

        builder.Services.AddSingleton<IHostedService>(sp =>
            new LoyaltyIngressConsumer(
                sp.GetRequiredService<IOptions<KafkaOptions>>(),
                sp.GetRequiredService<IOptionsMonitor<LoyaltyIngressOptions>>(),
                sp,
                sp.GetRequiredService<ILogger<LoyaltyIngressConsumer>>(),
                topic.Trim()));
    }
}

builder.Services.AddGrpc();


var app = builder.Build();

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment();
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<RPlus.Loyalty.Api.Services.LoyaltyGrpcService>();

// Ensure schema exists; skip migrations because snapshot is missing/obsolete.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
    await db.Database.EnsureCreatedAsync();
    await LoyaltySchemaBootstrapper.ApplyAsync(db);
    // NOTE: Tenure recalculation moved to cron schedule only (Loyalty__Cron__Enabled=true).
    // Running at startup caused data loss when HR service was unavailable.
}

app.Run();
