using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.Auth.Infrastructure;
using RPlus.Auth.Application;
using RPlus.Auth.Api.Services;
using RPlus.Auth.Options;
using RPlus.SDK.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using RPlus.Auth.Api.Schema;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets from HashiCorp Vault KV v2 (must be before any service reads config)
builder.Configuration.AddVault("auth");

// Options
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration("Jwt")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// OTP settings (debug code is disabled by default; enable explicitly for local/dev).
builder.Services.AddOptions<OtpOptions>()
    .BindConfiguration(OtpOptions.SectionName)
    .ValidateOnStart();

// Module registrations
builder.Services.AddAuthApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddGrpc();
builder.Services.AddControllers();

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "auth";
    options.ApplicationId = "auth";
    options.AccessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});

// v2: publish event schemas to Kafka (source) + Redis (cache)
builder.Services.AddSingleton<RPlus.SDK.Eventing.SchemaRegistry.IEventSchemaSource, AuthEventSchemaSource>();
builder.Services.AddEventSchemaRegistryPublisher(builder.Configuration);

builder.WebHost.ConfigureKestrel(options =>
{
    // Auth uses 5006 for HTTP1 and 5007 for gRPC/HTTP2
    options.ListenAnyIP(5006, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5007, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});


var app = builder.Build();

// Ensure the database schema exists; no migrations are defined for Auth yet.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseRouting();

app.MapControllers();
app.MapGrpcService<AuthKeyGrpcService>();
app.MapGrpcService<AuthGrpcService>();

app.Run();
