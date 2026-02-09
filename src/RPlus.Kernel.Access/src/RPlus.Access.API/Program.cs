using Confluent.Kafka;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using RPlus.Access.Application;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Api.Authentication;
using RPlus.Access.Api.Services;
using RPlus.Access.Application.Interfaces.Monitoring;
using RPlus.Access.Application.Services;
using RPlus.Access.Infrastructure.Clients;
using RPlus.Access.Infrastructure.Monitoring;
using RPlus.Access.Infrastructure.Services;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Infrastructure;
using RPlus.SDK.Infrastructure.Extensions;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Services;
using RPlusGrpc.Audit;
using StackExchange.Redis;
using RPlus.SDK.Infrastructure.Integration;
using RPlusGrpc.Auth;
using RPlusGrpc.Integration.Admin;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("access");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // Split ports to avoid Kestrel disabling HTTP/2 when HTTP/1.1 is also enabled without TLS.
    // - 5002: HTTP/1.1 (controllers, Swagger)
    // - 5003: HTTP/2 (gRPC, h2c inside docker network)
    options.ListenAnyIP(5002, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5003, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});

// Configure Serilog
builder.Host.UseSerilog((context, logger) => 
    logger.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

// Add Framework Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGrpc();

// Dynamic permission discovery: Access also publishes its own permissions so the admin panel can see them.
builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "access";
    options.ApplicationId = "access";
    options.AccessGrpcAddress = builder.Configuration["Services:Access:Grpc"] ?? "http://rplus-kernel-access:5003";
    options.SharedSecret = builder.Configuration["Access:PermissionManifest:SharedSecret"];
});

// Add Database
var connectionString = builder.Configuration["DB_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=rplus-kernel-db;Database=access;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AccessDbContext>(options =>
    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("RPlus.Access.Infrastructure"))
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
           .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IAccessDbContext>(sp => sp.GetRequiredService<AccessDbContext>());

// Add Application Layer
builder.Services.AddApplication();

// JWT validation for protected HTTP endpoints (admin APIs) + future internal calls.
var keyCache = new JwtKeyCache();
builder.Services.AddSingleton(keyCache);
builder.Services.AddHostedService<JwtKeyFetchService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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

builder.Services.AddTransient<IClaimsTransformation, AccessClaimsTransformation>();
builder.Services.AddAuthorization();
builder.Services.Configure<PermissionManifestOptions>(builder.Configuration.GetSection("Access:PermissionManifest"));

// Dependencies used by Access gRPC service + access evaluation
builder.Services.AddMemoryCache();

var redisConnectionString =
    builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["ConnectionStrings:Redis"]
    ?? "localhost:6379,abortConnect=false";

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IIntegRateLimiter, IntegRateLimiter>();
builder.Services.AddSingleton<IAccessMetrics, AccessMetrics>();
builder.Services.AddScoped<IRootAccessService, RootAccessService>();
builder.Services.AddHttpClient<IOrganizationClient, HttpOrganizationClient>(client =>
{
    var orgBase = builder.Configuration["Services:Organization"]
                  ?? "http://rplus-kernel-organization:5009/";
    client.BaseAddress = new Uri(orgBase);
});

builder.Services.AddGrpcClient<AuditService.AuditServiceClient>(o =>
{
    var auditAddress = builder.Configuration["Services:Audit:Grpc"]
                       ?? "http://rplus-kernel-audit:5010";
    o.Address = new Uri(auditAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

builder.Services.AddGrpcClient<AuthKeyService.AuthKeyServiceClient>(o =>
{
    var authGrpcAddress =
        builder.Configuration["Services:Auth:Grpc"]
        ?? builder.Configuration["AUTH_GRPC_ADDRESS"]
        ?? $"http://{builder.Configuration["AUTH_GRPC_HOST"] ?? "rplus-kernel-auth"}:{builder.Configuration["AUTH_GRPC_PORT"] ?? "5007"}";

    o.Address = new Uri(authGrpcAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

builder.Services.AddGrpcClient<IntegrationAdminService.IntegrationAdminServiceClient>(o =>
{
    var integrationGrpcAddress =
        builder.Configuration["Services:Integration:Grpc"]
        ?? builder.Configuration["INTEGRATION_GRPC_ADDRESS"]
        ?? $"http://{builder.Configuration["INTEGRATION_GRPC_HOST"] ?? "rplus-kernel-integration"}:{builder.Configuration["INTEGRATION_GRPC_PORT"] ?? "5013"}";

    o.Address = new Uri(integrationGrpcAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

builder.Services.Configure<IntegrationAdminClientOptions>(builder.Configuration.GetSection("Services:Integration:Admin"));
builder.Services.AddScoped<IIntegrationAdminClient, IntegrationAdminClient>();

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


var app = builder.Build();

// Production: apply EF migrations (no EnsureCreated, avoids schema drift).
await app.ApplyDatabaseMigrationsAsync(throwOnFailure: true, typeof(AccessDbContext));

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment();
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RPlus.Access.Api.Authentication.RequiresPermissionMiddleware>();
app.MapGrpcService<RPlus.Access.Api.Services.AccessGrpcService>();
app.MapControllers();

app.Run();
