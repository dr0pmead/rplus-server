using System;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.Organization.Api.Authentication;
using RPlus.Organization.Api.Workers;
using RPlus.Organization.Api.Services;
using RPlus.Organization.Application;
using RPlus.Organization.Infrastructure.Persistence;
using RPlus.Organization.Infrastructure;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Services;
using RPlus.Core.Options;
using Confluent.Kafka;
using RPlusGrpc.Hr;
using RPlus.SDK.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;
using Microsoft.OpenApi.Models;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("organization");

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RPlus.Organization API", Version = "v1" });
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.ListenAnyIP(5009, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
});

builder.Services.AddGrpcClient<HrService.HrServiceClient>(options =>
{
    var host = builder.Configuration["HR_GRPC_HOST"] ?? "rplus-kernel-hr";
    var port = builder.Configuration["HR_GRPC_PORT"] ?? "5010";
    options.Address = new Uri($"http://{host}:{port}");
});

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "organization";
    options.ApplicationId = "organization";
    options.AccessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaOptions = builder.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>()
                       ?? new KafkaOptions();
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaOptions.BootstrapServers
    };
    return new ProducerBuilder<string, string>(config).Build();
});
builder.Services.AddSingleton<IUserProfileProvider, UserProfileProvider>();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddKernelServiceDefaults(builder.Configuration);

builder.Services.Configure<ServiceSecretAuthenticationOptions>(builder.Configuration.GetSection("Organization:Auth"));

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

await app.ApplyDatabaseMigrationsAsync(throwOnFailure: true, typeof(OrganizationDbContext));

app.MapControllers();
app.Run();
