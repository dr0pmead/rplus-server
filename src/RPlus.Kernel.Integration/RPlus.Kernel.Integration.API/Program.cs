using Confluent.Kafka;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Api.Workers;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.Kernel.Integration.Infrastructure;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using RPlus.Kernel.Integration.Infrastructure.Services;
using RPlus.SDK.Infrastructure.Integration;
using RPlus.SDK.Infrastructure.Services;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using RPlus.Kernel.Integration.Api.Services;
using RPlus.Kernel.Integration.Application.Features.Partners.Commands;
using RPlus.Kernel.Integration.Api.Schema;
using RPlusGrpc.Access;
using RPlusGrpc.Audit;
using RPlusGrpc.Auth;
using RPlusGrpc.Meta;
using System.Net;
using System.Net.Http;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("integration");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // Split ports to avoid Kestrel disabling HTTP/2 when HTTP/1.1 is also enabled without TLS.
    // - 5008: HTTP/1.1 (controllers)
    // - 5013: HTTP/2 (gRPC, h2c inside docker network)
    options.ListenAnyIP(5008, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5013, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddIntegrationApplication();
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreatePartnerCommandHandler).Assembly);
});
builder.Services.AddIntegrationInfrastructure(builder.Configuration);

// Proactive caching: aggregator for cold start fallback (uses API-layer clients)
builder.Services.AddScoped<RPlus.Kernel.Integration.Api.Services.IScanProfileAggregator, RPlus.Kernel.Integration.Api.Services.ScanProfileAggregator>();

builder.Services.AddGrpc();

// Persist DataProtection keys to a shared volume to keep secrets decryptable across restarts
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"] ?? "/var/lib/rplus/integration-keys";
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName("rplus");

builder.Services.AddGrpcClient<AccessService.AccessServiceClient>(o =>
{
    var host = builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access";
    // Access gRPC runs on :5003 (HTTP/2). :5002 is HTTP/1.1 for controllers/Swagger.
    var port = builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003";
    o.Address = new Uri($"http://{host}:{port}");
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    // Force h2c prior knowledge; Kestrel doesn't support h2c upgrade.
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
});

builder.Services.AddGrpcClient<AuditService.AuditServiceClient>(o =>
{
    var auditAddress =
        builder.Configuration["Services:Audit:Grpc"]
        ?? builder.Configuration["AUDIT_GRPC_ADDRESS"]
        ?? $"http://{builder.Configuration["AUDIT_GRPC_HOST"] ?? "rplus-kernel-audit"}:{builder.Configuration["AUDIT_GRPC_PORT"] ?? "5010"}";

    o.Address = new Uri(auditAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    // Force h2c prior knowledge; Kestrel doesn't support h2c upgrade.
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
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
    // Force h2c prior knowledge; Kestrel doesn't support h2c upgrade.
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
});


builder.Services.AddMemoryCache();

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
builder.Services.AddSingleton<RPlus.SDK.Eventing.SchemaRegistry.IEventSchemaSource, IntegrationEventSchemaSource>();
builder.Services.AddEventSchemaRegistryPublisher(builder.Configuration);

builder.Services.Configure<IntegrationScanOptions>(builder.Configuration.GetSection("Integration:Scan"));
builder.Services.Configure<IntegrationMetaOptions>(builder.Configuration.GetSection("Integration:Meta"));
builder.Services.Configure<IntegrationListSyncOptions>(builder.Configuration.GetSection("Integration:ListSync"));
builder.Services.Configure<IntegrationAdminGrpcOptions>(builder.Configuration.GetSection("Integration:Admin"));
builder.Services.Configure<IntegrationHrOptions>(builder.Configuration.GetSection("Integration:Hr"));
builder.Services.PostConfigure<IntegrationHrOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        options.BaseUrl =
            builder.Configuration["Services:Hr:BaseUrl"]
            ?? builder.Configuration["Services:Hr"]
            ?? "http://rplus-kernel-hr:5015/";
    }

    if (string.IsNullOrWhiteSpace(options.SharedSecret))
    {
        options.SharedSecret =
            builder.Configuration["HR:Auth:SharedSecret"]
            ?? builder.Configuration["HR__Auth__SharedSecret"]
            ?? builder.Configuration["RPLUS_INTERNAL_SERVICE_SECRET"];
    }
});
builder.Services.AddSingleton<IQrTokenStore, RedisQrTokenStore>();
builder.Services.AddSingleton<IQrTokenValidator, QrTokenValidator>();

// Short Code OTP fallback (for partner scan without QR scanner)
builder.Services.AddSingleton<IShortCodeValidator, ShortCodeValidator>();
builder.Services.AddSingleton<IUserTokenResolver, UserTokenResolver>();

// Partner scan dependencies
builder.Services.AddScoped<IPartnerApiKeyValidator, PartnerApiKeyValidator>();
builder.Services.AddSingleton<IAccessIntegrationPermissionService, AccessIntegrationPermissionService>();
builder.Services.AddScoped<IScanContextResolver, ScanContextResolver>();
builder.Services.AddScoped<IHrProfileClient, HrProfileClient>();
builder.Services.AddScoped<IScanFieldCatalogService, ScanFieldCatalogService>();
builder.Services.AddScoped<IMetaListLookupService, MetaListLookupService>();
builder.Services.AddScoped<IScanFieldResolver, ScanFieldResolver>();
builder.Services.AddScoped<IIntegrationListSyncService, IntegrationListSyncService>();

// Double Entry Partner Integration (Intent → Commit)
builder.Services.AddScoped<IPartnerIntegrationService, PartnerIntegrationService>();


builder.Services.AddGrpcClient<MetaService.MetaServiceClient>(o =>
{
    var metaGrpcAddress =
        builder.Configuration["Integration:Meta:GrpcAddress"]
        ?? builder.Configuration["Services:Meta:Grpc"]
        ?? "http://rplus-kernel-meta:5019";
    o.Address = new Uri(metaGrpcAddress);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
});
builder.Services.AddHttpClient<ILoyaltyProfileClient, LoyaltyProfileClient>(client =>
{
    var baseUrl =
        builder.Configuration["Services:Loyalty:BaseUrl"]
        ?? builder.Configuration["Services:Loyalty"]
        ?? "http://rplus-kernel-loyalty:5012/";

    client.BaseAddress = new Uri(baseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
});

builder.Services.AddHttpClient<IHrProfileClient, HrProfileClient>(client =>
{
    var baseUrl =
        builder.Configuration["Integration:Hr:BaseUrl"]
        ?? builder.Configuration["Services:Hr:BaseUrl"]
        ?? builder.Configuration["Services:Hr"]
        ?? "http://rplus-kernel-hr:5015/";

    client.BaseAddress = new Uri(baseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
});

builder.Services.AddHttpClient("audit-http", client =>
{
    var auditHost = builder.Configuration["AUDIT_HTTP_HOST"] ?? "rplus-kernel-audit";
    var auditPort = builder.Configuration["AUDIT_HTTP_PORT"] ?? "5011";
    var auditHttpAddress =
        builder.Configuration["Services:Audit:Http"]
        ?? builder.Configuration["AUDIT_HTTP_ADDRESS"]
        ?? $"http://{auditHost}:{auditPort}";

    client.BaseAddress = new Uri(auditHttpAddress);
});
builder.Services.AddScoped<IPartnerScanService, PartnerScanService>();
builder.Services.AddSingleton<IDiscountCalculator, DynamicLevelDiscountCalculator>();

builder.Services.AddHostedService<PermissionRegistrarWorker>();

// Background job for expiring stale scans
builder.Services.AddHostedService<ScanCleanupService>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddExternalOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "integration";
    options.ApplicationId = "integration";
    options.AccessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});


var app = builder.Build();

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment();
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/external/swagger.json", "Integration external v1");
    });
}

app.MapGrpcService<IntegrationGrpcService>();
app.MapGrpcService<IntegrationAdminGrpcService>();
app.MapHealthChecks("/health");
app.MapControllers();

// Ensure schema exists before hosted services run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
    await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS integration;");
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS access_level text NOT NULL DEFAULT 'limited';");
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS metadata jsonb NOT NULL DEFAULT '{0}'::jsonb;",
        "{}");
    await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS integration.integration_stats (
  id BIGSERIAL PRIMARY KEY,
  partner_id uuid NOT NULL,
  key_id uuid NOT NULL,
  environment text NOT NULL,
  scope text NOT NULL,
  endpoint text NOT NULL,
  status_code integer NOT NULL,
  latency_ms bigint NOT NULL,
  correlation_id text NOT NULL,
  error_code integer NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_created_at ON integration.integration_stats(created_at);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_partner_id ON integration.integration_stats(partner_id);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_key_id ON integration.integration_stats(key_id);");
    await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_scope ON integration.integration_stats(scope);");
    await db.Database.EnsureCreatedAsync();
}

app.Run();
