using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RPlus.HR.Api.Authorization;
using RPlus.HR.Api.Authentication;
using RPlus.HR.Api.Services;
using RPlus.HR.Infrastructure;
using RPlus.HR.Infrastructure.Persistence;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;
using RPlus.HR.Application.Interfaces;
using RPlusGrpc.Access;
using RPlus.Core.Options;
using RPlus.HR.Api.Consumers;
using RPlus.HR.Infrastructure.Services;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Services;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using RPlus.HR.Api.Schema;
using StackExchange.Redis;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("hr");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // 5015: HTTP/1.1 (controllers, Swagger)
    // 5016: HTTP/2 (gRPC, h2c inside docker network)
    options.ListenAnyIP(5015, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5016, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGrpc();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IHrActorContext, HttpContextHrActorContext>();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddHostedService<HrStaffUserProvisioningConsumer>();

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
builder.Services.AddSingleton<RPlus.SDK.Eventing.SchemaRegistry.IEventSchemaSource, HrEventSchemaSource>();
var redisConnectionString =
    builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["ConnectionStrings:Redis"]
    ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddEventSchemaRegistryPublisher(builder.Configuration);

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "hr";
    options.ApplicationId = "hr";
    options.AccessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKernelServiceDefaults(builder.Configuration);
builder.Services.AddHostedService<SystemHrFieldSeeder>();

builder.Services.Configure<ServiceSecretAuthenticationOptions>(builder.Configuration.GetSection("HR:Auth"));
builder.Services.AddScoped<DocumentsGateway>();

var keyCache = new JwtKeyCache();
builder.Services.AddSingleton(keyCache);
builder.Services.AddHostedService<JwtKeyFetchService>();

builder.Services
    .AddAuthentication("Smart")
    .AddPolicyScheme("Smart", "Smart", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey("x-rplus-service-secret")
                ? ServiceSecretAuthenticationHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, ServiceSecretAuthenticationHandler>(ServiceSecretAuthenticationHandler.SchemeName, _ => { })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = false;

        var configuration = builder.Configuration;
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

builder.Services.AddScoped<HrGrpcService>();

builder.Services.AddGrpcClient<AccessService.AccessServiceClient>(o =>
{
    var accessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";

    o.Address = new Uri(accessGrpcAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

builder.Services.AddGrpcClient<RPlusGrpc.Loyalty.LoyaltyService.LoyaltyServiceClient>(o =>
{
    var loyaltyGrpcAddress =
        builder.Configuration["Services:Loyalty:Grpc"]
        ?? $"http://{builder.Configuration["LOYALTY_GRPC_HOST"] ?? "rplus-kernel-loyalty"}:{builder.Configuration["LOYALTY_GRPC_PORT"] ?? "5013"}";

    o.Address = new Uri(loyaltyGrpcAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});


var app = builder.Build();
app.UseKernelServiceDefaults();

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment();
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequiresPermissionMiddleware>();

await app.ApplyDatabaseMigrationsAsync(throwOnFailure: true, typeof(HrDbContext));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE IF EXISTS hr.hr_profiles
            ADD COLUMN IF NOT EXISTS documents_folder_id uuid;
        ALTER TABLE IF EXISTS hr.hr_files
            ADD COLUMN IF NOT EXISTS document_id uuid;
        """);
}

app.MapControllers();
app.MapGrpcService<HrGrpcService>();
app.Run();
