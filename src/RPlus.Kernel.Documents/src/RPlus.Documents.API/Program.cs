using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RPlus.Documents.Api.Authentication;
using RPlus.Documents.Api.Authorization;
using RPlus.Documents.Api.Options;
using RPlus.Documents.Api.Services;
using RPlus.Documents.Infrastructure;
using RPlus.Documents.Infrastructure.Persistence;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.Access.PermissionDiscovery;
using RPlus.SDK.Infrastructure.Extensions;
using RPlusGrpc.Access;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Security.Cryptography.X509Certificates;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("documents");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    var mtlsEnabled = builder.Configuration.GetValue("Documents:Mtls:Enabled", false)
                      || builder.Configuration.GetValue("Documents__Mtls__Enabled", false);

    if (!mtlsEnabled)
    {
        options.ListenAnyIP(5017, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
        return;
    }

    var certPath = builder.Configuration["Documents:Tls:CertificatePath"]
                   ?? builder.Configuration["Documents__Tls__CertificatePath"];
    if (string.IsNullOrWhiteSpace(certPath))
        throw new InvalidOperationException("Documents mTLS requires Documents:Tls:CertificatePath.");

    var certPassword = builder.Configuration["Documents:Tls:CertificatePassword"]
                       ?? builder.Configuration["Documents__Tls__CertificatePassword"];

    var cert = X509CertificateLoader.LoadPkcs12FromFile(
        certPath,
        certPassword,
        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet |
        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);

    var allowedThumbprints = builder.Configuration.GetSection("Documents:Mtls:AllowedThumbprints").Get<string[]>()
                             ?? Array.Empty<string>();

    options.ListenAnyIP(5017, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
        listenOptions.UseHttps(new HttpsConnectionAdapterOptions
        {
            ServerCertificate = cert,
            ClientCertificateMode = ClientCertificateMode.RequireCertificate,
            ClientCertificateValidation = (clientCert, _, _) =>
            {
                if (clientCert == null)
                    return false;

                if (allowedThumbprints.Length == 0)
                    return true;

                var thumbprint = clientCert.Thumbprint?.Replace(":", string.Empty).ToUpperInvariant();
                return allowedThumbprints.Any(tp =>
                    string.Equals(tp.Replace(":", string.Empty), thumbprint, StringComparison.OrdinalIgnoreCase));
            }
        });
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<DocumentUploadOptions>(builder.Configuration.GetSection(DocumentUploadOptions.SectionName));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRPlusPermissionManifestPublisher(builder.Configuration, options =>
{
    options.ServiceName = "documents";
    options.ApplicationId = "documents";
    options.AccessGrpcAddress =
        builder.Configuration["Services:Access:Grpc"]
        ?? $"http://{builder.Configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{builder.Configuration["ACCESS_GRPC_PORT"] ?? "5003"}";
    options.SharedSecret =
        builder.Configuration["Access:PermissionManifest:SharedSecret"]
        ?? builder.Configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];
});

builder.Services.AddDocumentsInfrastructure(builder.Configuration);
builder.Services.AddKernelServiceDefaults(builder.Configuration);

builder.Services.Configure<ServiceSecretAuthenticationOptions>(builder.Configuration.GetSection("Documents:Auth"));

var keyCache = new JwtKeyCache();
builder.Services.AddSingleton(keyCache);

var staticPublicKeyPem =
    builder.Configuration["JWT__PUBLIC_KEY_PEM"]
    ?? builder.Configuration["Jwt__PublicKeyPem"];

if (!string.IsNullOrWhiteSpace(staticPublicKeyPem))
{
    try
    {
        var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(staticPublicKeyPem.AsSpan());
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = "static" };
        keyCache.UpdateKeys(new[] { rsaKey });
    }
    catch (Exception ex)
    {
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Documents.JwtKey");
        logger.LogWarning(ex, "Failed to import JWT__PUBLIC_KEY_PEM. Falling back to Auth gRPC.");
        builder.Services.AddHostedService<JwtKeyFetchService>();
    }
}
else
{
    builder.Services.AddHostedService<JwtKeyFetchService>();
}

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

var internalSecret =
    app.Configuration["Documents:Internal:SharedSecret"]
    ?? app.Configuration["Documents__Internal__SharedSecret"]
    ?? app.Configuration["RPLUS_INTERNAL_SERVICE_SECRET"];

app.Use(async (context, next) =>
{
    if (string.IsNullOrWhiteSpace(internalSecret))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "internal_secret_missing" });
        return;
    }

    if (!context.Request.Headers.TryGetValue("X-RPlus-Internal", out var provided) ||
        string.IsNullOrWhiteSpace(provided) ||
        !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided.ToString()),
            System.Text.Encoding.UTF8.GetBytes(internalSecret)))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "forbidden" });
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
var enforcePermissions = app.Configuration.GetValue("Documents:Authorization:Enforce", true);
if (enforcePermissions)
{
    app.UseMiddleware<RequiresPermissionMiddleware>();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
    await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS documents;");
        await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS storage_key text NOT NULL DEFAULT '';
        ALTER TABLE IF EXISTS documents.document_files
            ALTER COLUMN data DROP NOT NULL;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS folder_id uuid;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS organization_id uuid;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS department_id uuid;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS subject_type text;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS subject_id uuid;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS document_type text;
        ALTER TABLE IF EXISTS documents.document_files
            ADD COLUMN IF NOT EXISTS is_locked boolean NOT NULL DEFAULT false;
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS documents.document_folders (
            id uuid PRIMARY KEY,
            owner_user_id uuid NOT NULL,
            organization_id uuid NULL,
            department_id uuid NULL,
            name text NOT NULL,
            type text NOT NULL DEFAULT 'Project',
            is_system boolean NOT NULL DEFAULT false,
            is_immutable boolean NOT NULL DEFAULT false,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS documents.document_folder_members (
            id uuid PRIMARY KEY,
            folder_id uuid NOT NULL,
            user_id uuid NOT NULL,
            is_owner boolean NOT NULL DEFAULT false,
            can_view boolean NOT NULL DEFAULT false,
            can_download boolean NOT NULL DEFAULT false,
            can_upload boolean NOT NULL DEFAULT false,
            can_edit boolean NOT NULL DEFAULT false,
            can_delete_files boolean NOT NULL DEFAULT false,
            can_delete_folder boolean NOT NULL DEFAULT false,
            can_share_links boolean NOT NULL DEFAULT false,
            can_manage_members boolean NOT NULL DEFAULT false,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS documents.document_shares (
            id uuid PRIMARY KEY,
            document_id uuid NOT NULL,
            granted_to_user_id uuid NULL,
            expires_at timestamptz NOT NULL,
            max_downloads integer NULL,
            download_count integer NOT NULL DEFAULT 0,
            created_by_user_id uuid NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        """);
    await db.Database.EnsureCreatedAsync();
}

app.MapControllers();
app.Run();
