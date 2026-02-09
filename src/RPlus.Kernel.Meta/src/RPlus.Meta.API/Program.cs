using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.Meta.Api.Authentication;
using RPlus.Meta.Api.Authorization;
using RPlus.Meta.Api.Services;
using RPlus.Meta.Infrastructure;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;
using RPlus.SDK.Infrastructure.Extensions;
using RPlusGrpc.Access;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("meta");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.ListenAnyIP(5018, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5019, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "meta";
    options.ApplicationId = "meta";
    options.AccessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});

builder.Services.AddMetaInfrastructure(builder.Configuration);
builder.Services.AddKernelServiceDefaults(builder.Configuration);
builder.Services.AddHostedService<SystemMetaSeeder>();

builder.Services.Configure<ServiceSecretAuthenticationOptions>(builder.Configuration.GetSection("Meta:Auth"));

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MetaDbContext>();
    await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS meta;");

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS meta.meta_entity_types (
            id uuid PRIMARY KEY,
            key text NOT NULL,
            title text NOT NULL,
            description text NULL,
            is_system boolean NOT NULL DEFAULT false,
            is_active boolean NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_entity_types_key ON meta.meta_entity_types (key);
        
        CREATE TABLE IF NOT EXISTS meta.meta_field_definitions (
            id uuid PRIMARY KEY,
            entity_type_id uuid NOT NULL,
            key text NOT NULL,
            title text NOT NULL,
            data_type text NOT NULL,
            ""order"" integer NOT NULL DEFAULT 0,
            is_required boolean NOT NULL DEFAULT false,
            is_system boolean NOT NULL DEFAULT false,
            is_active boolean NOT NULL DEFAULT true,
            options_json jsonb NULL,
            validation_json jsonb NULL,
            reference_source_json jsonb NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_field_definitions_entity_key ON meta.meta_field_definitions (entity_type_id, key);

        CREATE TABLE IF NOT EXISTS meta.meta_field_types (
            id uuid PRIMARY KEY,
            key text NOT NULL,
            title text NOT NULL,
            description text NULL,
            ui_schema_json jsonb NULL,
            is_system boolean NOT NULL DEFAULT false,
            is_active boolean NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_field_types_key ON meta.meta_field_types (key);

        CREATE TABLE IF NOT EXISTS meta.meta_entity_records (
            id uuid PRIMARY KEY,
            entity_type_id uuid NOT NULL,
            subject_type text NULL,
            subject_id uuid NULL,
            owner_user_id uuid NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now()
        );
        
        CREATE TABLE IF NOT EXISTS meta.meta_field_values (
            id uuid PRIMARY KEY,
            record_id uuid NOT NULL,
            field_id uuid NOT NULL,
            value_json jsonb NOT NULL,
            updated_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_field_values_record_field ON meta.meta_field_values (record_id, field_id);

        CREATE TABLE IF NOT EXISTS meta.meta_relations (
            id uuid PRIMARY KEY,
            from_record_id uuid NOT NULL,
            to_record_id uuid NOT NULL,
            relation_type text NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_relations_unique ON meta.meta_relations (from_record_id, to_record_id, relation_type);

        CREATE TABLE IF NOT EXISTS meta.meta_lists (
            id uuid PRIMARY KEY,
            entity_type_id uuid NULL,
            key text NOT NULL,
            title text NOT NULL,
            description text NULL,
            sync_mode text NOT NULL DEFAULT 'manual',
            is_system boolean NOT NULL DEFAULT false,
            is_active boolean NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        ALTER TABLE meta.meta_lists ADD COLUMN IF NOT EXISTS entity_type_id uuid NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_lists_key ON meta.meta_lists (key);
        CREATE INDEX IF NOT EXISTS ix_meta_lists_entity_type ON meta.meta_lists (entity_type_id);

        CREATE TABLE IF NOT EXISTS meta.meta_list_items (
            id uuid PRIMARY KEY,
            list_id uuid NOT NULL,
            code text NOT NULL,
            title text NOT NULL,
            value_json jsonb NULL,
            external_id text NULL,
            is_active boolean NOT NULL DEFAULT true,
            ""order"" integer NOT NULL DEFAULT 0,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_list_items_list_code ON meta.meta_list_items (list_id, code);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_meta_list_items_list_external_id ON meta.meta_list_items (list_id, external_id) WHERE external_id IS NOT NULL;
    ");

    await db.Database.EnsureCreatedAsync();
}

app.MapControllers();
app.MapGrpcService<MetaGrpcService>();
app.Run();
