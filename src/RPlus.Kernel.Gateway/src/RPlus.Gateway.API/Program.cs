using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RPlus.SDK.Infrastructure.DependencyInjection;
using RPlus.Gateway.Api.Services;
using RPlusGrpc.Guard;
using RPlusGrpc.Auth;
using RPlusGrpc.Access;
using RPlusGrpc.Wallet;
using RPlusGrpc.Users;
using RPlusGrpc.Integration;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Gateway.Api.Realtime;
using RPlus.SDK.Eventing;
using RPlus.SDK.Gateway.Realtime;
using RPlus.Gateway.Api.Proxy;
using StackExchange.Redis;
using Yarp.ReverseProxy.Forwarder;
using Microsoft.OpenApi.Models;
using RPlus.Gateway.Api.OpenApi;
using RPlus.Gateway.Api.Auth;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("gateway");

var tlsMode = builder.Configuration["Gateway:Tls:Mode"]
              ?? builder.Configuration["Gateway__Tls__Mode"]
              ?? "Disabled";

var redirectHttpToHttps = builder.Configuration.GetValue<bool?>("Gateway:Tls:RedirectHttpToHttps")
                         ?? builder.Configuration.GetValue<bool?>("Gateway__Tls__RedirectHttpToHttps")
                         ?? true;

X509Certificate2? httpsCertificate = null;
if (!tlsMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
{
    httpsCertificate = tlsMode.Equals("Provided", StringComparison.OrdinalIgnoreCase)
        ? LoadProvidedCertificate(builder.Configuration)
        : CreateSelfSignedLocalhostCertificate();
}

builder.WebHost.ConfigureKestrel(options =>
{
    // Canonical local URL expected by clients: http://localhost/api/{service}/{method}
    options.AddServerHeader = false;
    options.ListenAnyIP(80, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });

    if (httpsCertificate is not null)
    {
        options.ListenAnyIP(443, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            listenOptions.UseHttps(new HttpsConnectionAdapterOptions
            {
                ServerCertificate = httpsCertificate
            });
        });
    }
});

// Add RPlus Module (SDK)
builder.Services.AddRPlusModule<RPlus.Gateway.Application.GatewayModuleManifest>(builder.Configuration);

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("OpenApiProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.Configure<OpenApiProxyOptions>(builder.Configuration.GetSection(OpenApiProxyOptions.SectionName));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RPlus Gateway API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.Configure<ProxyAuthorizationOptions>(builder.Configuration.GetSection("Gateway:Authorization"));
builder.Services.AddScoped<ProxyAuthorizationService>();
builder.Services.AddScoped<IntegrationKeyAuthorizationService>();
builder.Services.AddScoped<PermissionGuard>();
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));

// CORS (fail-closed in production unless explicitly configured)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowCredentials()
                .AllowAnyHeader()
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .WithExposedHeaders(HeaderNames.Location);
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        return false;

                    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                           uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                })
                .AllowCredentials()
                .AllowAnyHeader()
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .WithExposedHeaders(HeaderNames.Location);
        }
    });
});

if (httpsCertificate is not null && redirectHttpToHttps)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.HttpsPort = 443;
    });
}

// gRPC clients
var guardGrpcEndpoint = builder.Configuration["Services:Guard:Grpc"] ?? "http://rplus-kernel-guard:5013";
builder.Services.AddGrpcClient<GuardService.GuardServiceClient>(o =>
{
    o.Address = new Uri(guardGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

var authGrpcEndpoint = builder.Configuration["Services:Auth:Grpc"] ?? "http://rplus-kernel-auth:5007";
builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(o =>
{
    o.Address = new Uri(authGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

builder.Services.AddGrpcClient<AuthKeyService.AuthKeyServiceClient>(o =>
{
    o.Address = new Uri(authGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

var accessGrpcEndpoint = builder.Configuration["Services:Access:Grpc"] ?? "http://rplus-kernel-access:5003";
builder.Services.AddGrpcClient<AccessService.AccessServiceClient>(o =>
{
    o.Address = new Uri(accessGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

var walletGrpcEndpoint = builder.Configuration["Services:Wallet:Grpc"] ?? "http://rplus-kernel-wallet:5005";
builder.Services.AddGrpcClient<WalletService.WalletServiceClient>(o =>
{
    o.Address = new Uri(walletGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

var usersGrpcEndpoint = builder.Configuration["Services:Users:Grpc"] ?? "http://rplus-kernel-users:5015";
builder.Services.AddGrpcClient<UsersService.UsersServiceClient>(o =>
{
    o.Address = new Uri(usersGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
});

var integrationGrpcEndpoint = builder.Configuration["Services:Integration:Grpc"] ?? "http://rplus-kernel-integration:5013";
builder.Services.AddGrpcClient<IntegrationService.IntegrationServiceClient>(o =>
{
    o.Address = new Uri(integrationGrpcEndpoint);
})
.ConfigureHttpClient(client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
});

// JWT validation (required for protected endpoints + proxy calls)
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

        // Cookie auth support (HttpOnly cookies). Keep Bearer headers working for non-browser clients.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var cookies = AuthCookieOptions.From(context.HttpContext.RequestServices.GetRequiredService<IConfiguration>());
                var path = context.HttpContext.Request.Path;

                // Realtime MUST authenticate via HttpOnly cookies only (no Bearer token support).
                if (path.StartsWithSegments("/api/realtime", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/realtime", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.HttpContext.Request.Cookies.TryGetValue(cookies.AccessTokenCookieName, out var token) &&
                        !string.IsNullOrWhiteSpace(token))
                    {
                        context.Token = token;
                    }
                    else
                    {
                        // Backward-compat
                        if (context.HttpContext.Request.Cookies.TryGetValue("access_token", out var legacyToken) &&
                            !string.IsNullOrWhiteSpace(legacyToken))
                        {
                            context.Token = legacyToken;
                        }
                        else
                        {
                        context.NoResult();
                        }
                    }

                    return Task.CompletedTask;
                }

                // For normal API endpoints:
                // - Prefer Authorization: Bearer <token>
                // - Fallback to cookie if Authorization header is absent
                if (string.IsNullOrWhiteSpace(context.Token))
                {
                    if (context.HttpContext.Request.Cookies.TryGetValue(cookies.AccessTokenCookieName, out var token) &&
                        !string.IsNullOrWhiteSpace(token))
                    {
                        context.Token = token;
                    }
                    else if (context.HttpContext.Request.Cookies.TryGetValue("access_token", out var legacyToken) &&
                             !string.IsNullOrWhiteSpace(legacyToken))
                    {
                        context.Token = legacyToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Anonymous", p => p.RequireAssertion(_ => true));
    options.AddPolicy("Default", p => p.RequireAuthenticatedUser());
});

// Minimal rate limiting for public endpoints (protects gRPC backends)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        return ValueTask.CompletedTask;
    };

    static string ResolveClientKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("x-device-id", out var deviceId) &&
            !string.IsNullOrWhiteSpace(deviceId))
        {
            return $"device:{deviceId.ToString().Trim()}";
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(ip))
                return $"ip:{ip}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    // Global safety net to mitigate basic DDoS patterns (per device/IP).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientKey = ResolveClientKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(
            clientKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromSeconds(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddPolicy("public", context =>
    {
        var clientKey = ResolveClientKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(
            clientKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromSeconds(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
         });
    });

    // Stricter limits for brute-force-prone endpoints.
    options.AddPolicy("auth-identify", context =>
    {
        var ip = ResolveClientKey(context);
        return RateLimitPartition.GetTokenBucketLimiter(
            ip,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 15,
                TokensPerPeriod = 15,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("auth-login", context =>
    {
        var ip = ResolveClientKey(context);
        return RateLimitPartition.GetTokenBucketLimiter(
            ip,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 6,
                TokensPerPeriod = 6,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("auth-otp", context =>
    {
        var ip = ResolveClientKey(context);
        return RateLimitPartition.GetTokenBucketLimiter(
            ip,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 8,
                TokensPerPeriod = 8,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

// Reverse proxy + forwarder (dynamic service selection under /api/{service}/{method})
builder.Services.AddReverseProxy();

// Realtime (Kafka projection -> Redis fanout -> SSE) - enabled via feature flag only.
var realtimeConfig = builder.Configuration.GetSection("Realtime").Get<RealtimeGatewayOptions>() ?? new RealtimeGatewayOptions();
if (realtimeConfig.Enabled)
{
    builder.Services.Configure<RealtimeGatewayOptions>(builder.Configuration.GetSection("Realtime"));
    builder.Services.AddSingleton<IRealtimeEventMapper, RealtimeEventMapper>();
    builder.Services.AddSingleton<IRealtimeEventHub, InMemoryRealtimeEventHub>();
    builder.Services.AddSingleton<IRealtimePolicyService, AccessRealtimePolicyService>();

    var redisConnectionString =
        builder.Configuration["Redis:ConnectionString"]
        ?? builder.Configuration["Redis__ConnectionString"]
        ?? builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["ConnectionStrings:Redis"]
        ?? string.Empty;

    if (string.IsNullOrWhiteSpace(redisConnectionString))
        throw new InvalidOperationException("Realtime requires Redis (Redis:ConnectionString).");

    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnectionString));

    builder.Services.AddSingleton<IRealtimeFanoutPublisher, RedisRealtimeFanoutPublisher>();
    builder.Services.AddHostedService<RealtimeRedisFanoutService>();

    // Consume allowlisted topics as EventEnvelope<JsonElement> and publish projections to fanout.
    builder.Services.AddScoped<IKafkaConsumer<EventEnvelope<JsonElement>>, RealtimeKafkaEnvelopeConsumer>();

    var kafka = builder.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
    var bootstrap = kafka.BootstrapServers;
    var groupId = string.IsNullOrWhiteSpace(realtimeConfig.Kafka.GroupId) ? "rplus-realtime-gateway" : realtimeConfig.Kafka.GroupId;

    foreach (var topic in realtimeConfig.Kafka.Topics.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal))
    {
        builder.Services.AddSingleton<IHostedService>(sp =>
            new KafkaConsumerService<EventEnvelope<System.Text.Json.JsonElement>>(sp, bootstrap, topic.Trim(), groupId));
    }
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment();
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway v1");

        var proxy = app.Configuration.GetSection(OpenApiProxyOptions.SectionName).Get<OpenApiProxyOptions>() ?? new OpenApiProxyOptions();
        if (proxy.Enabled && proxy.Services.Count > 0)
        {
            foreach (var kv in proxy.Services.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var service = kv.Key;
                var doc = string.IsNullOrWhiteSpace(kv.Value.DocName) ? "v1" : kv.Value.DocName.Trim();
                c.SwaggerEndpoint($"/openapi/{service}/{doc}/swagger.json", $"{service} ({doc})");
            }
        }
    });
}

// If running behind a reverse proxy, configure KnownNetworks/KnownProxies to avoid spoofed X-Forwarded-* headers.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
});

if (httpsCertificate is not null)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    if (redirectHttpToHttps)
    {
        app.UseHttpsRedirection();
    }
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();

// Realtime endpoint must reject Bearer auth headers (cookie-only auth).
if (realtimeConfig.Enabled)
{
    app.UseWhen(
        ctx =>
        {
            var p = ctx.Request.Path;
            return p.StartsWithSegments("/api/realtime", StringComparison.OrdinalIgnoreCase) ||
                   p.Equals("/realtime", StringComparison.OrdinalIgnoreCase);
        },
        branch =>
        {
            branch.Use(async (ctx, next) =>
            {
                if (ctx.Request.Headers.ContainsKey("Authorization"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await ctx.Response.WriteAsJsonAsync(new { error = "forbidden_auth_channel" });
                    return;
                }

                await next();
            });
        });
}

app.UseAuthentication();
app.UseAuthorization();

// Simple discovery endpoint (helps clients connect consistently)
app.MapGet("/api", () => Results.Ok(new
{
    basePath = "/api",
    routes = new[]
    {
        "/api/auth/{method}",
        "/api/pow/{method}",
        "/api/access/{method}",
        "/api/{service}/{method}"
    }
})).AllowAnonymous();

if (realtimeConfig.Enabled)
{
    app.UseWebSockets();
    app.MapRealtimeEndpoints();
    app.MapRealtimeWebSocketEndpoint();
}
app.MapControllers();

// Dynamic proxy for internal services: http://localhost/api/{service}/{method}
// Protects against SSRF by requiring allowlisted upstreams from configuration.
var upstreams = BuildUpstreams(builder.Configuration);
var httpClient = CreateProxyHttpClient(null);
var documentsClientCert = LoadDocumentsClientCertificate(builder.Configuration);
var documentsHttpClient = documentsClientCert == null ? httpClient : CreateProxyHttpClient(documentsClientCert);
var authCookies = AuthCookieOptions.From(builder.Configuration);

var documentsBase = builder.Configuration["Gateway:Upstreams:documents:BaseAddress"]
                     ?? builder.Configuration["Gateway__Upstreams__documents__BaseAddress"];
var internalSecret = builder.Configuration["Gateway:Internal:SharedSecret"]
                    ?? builder.Configuration["Gateway__Internal__SharedSecret"]
                    ?? builder.Configuration["RPLUS_INTERNAL_SERVICE_SECRET"];

var proxyTransformer = new CookieAuthHeaderTransformer(authCookies, documentsBase, internalSecret);

// Allow CORS preflight without auth for proxied endpoints.
((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app)
    // Require a non-empty catchAll so that first-class endpoints like /api/users (Gateway controller) don't get swallowed
    // by the generic proxy route (/api/{service}/{**catchAll}).
    .MapMethods("/api/{service}/{**catchAll:regex(.+)}", new[] { "OPTIONS" }, () => Results.NoContent());

((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).Map("/api/{service}/{**catchAll:regex(.+)}", async context =>
{
    var routeValues = context.Request.RouteValues;
    var service = routeValues.TryGetValue("service", out var raw) ? raw?.ToString() : null;
    if (string.IsNullOrWhiteSpace(service) ||
        service.Equals("auth", StringComparison.OrdinalIgnoreCase) ||
        service.Equals("pow", StringComparison.OrdinalIgnoreCase) ||
        service.Equals("v1", StringComparison.OrdinalIgnoreCase) ||
        service.Equals("admin", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "not_found" });
        return;
    }

    if (!upstreams.TryGetValue(service, out var destinationPrefix))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "unknown_service" });
        return;
    }

    var catchAll = routeValues.TryGetValue("catchAll", out var rawCatchAll) ? rawCatchAll?.ToString() : null;

    // Webhook bypass: Meta Cloud API sends POST webhooks without JWT.
    // GET is used for initial webhook verification handshake.
    // Security is handled by the downstream service itself (HMAC-SHA256 signature validation).
    var isWebhookBypass = service!.Equals("hunter", StringComparison.OrdinalIgnoreCase)
                          && catchAll is not null
                          && catchAll.Contains("webhook", StringComparison.OrdinalIgnoreCase)
                          && (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsGet(context.Request.Method));

    var integrationKey = context.Request.Headers["X-Integration-Key"].ToString();
    if (isWebhookBypass)
    {
        // Skip auth — forward directly. Downstream validates HMAC signature.
    }
    else if (!string.IsNullOrWhiteSpace(integrationKey))
    {
        var integrationAuth = context.RequestServices.GetRequiredService<IntegrationKeyAuthorizationService>();
        var authResult = await integrationAuth.AuthorizeAsync(context, service, catchAll, context.RequestAborted);
        if (!authResult.Allowed)
        {
            context.Response.StatusCode = authResult.Error switch
            {
                "missing_integration_key" => StatusCodes.Status401Unauthorized,
                "invalid_integration_key" => StatusCodes.Status401Unauthorized,
                "invalid_secret" => StatusCodes.Status401Unauthorized,
                "key_inactive" => StatusCodes.Status401Unauthorized,
                "key_expired" => StatusCodes.Status401Unauthorized,
                "rate_limit_exceeded" => StatusCodes.Status429TooManyRequests,
                "quota_exceeded" => StatusCodes.Status429TooManyRequests,
                "access_unavailable" => StatusCodes.Status503ServiceUnavailable,
                "access_error" => StatusCodes.Status502BadGateway,
                "integration_unavailable" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status403Forbidden
            };

            await context.Response.WriteAsJsonAsync(new
            {
                error = authResult.Error ?? "forbidden",
                permission = authResult.PermissionId
            });
            return;
        }

        if (authResult.Context is not null)
        {
            if (!string.IsNullOrWhiteSpace(authResult.Context.ApiKeyId))
            {
                context.Request.Headers["X-Integration-Key-Id"] = authResult.Context.ApiKeyId;
            }

            if (!string.IsNullOrWhiteSpace(authResult.Context.PartnerId))
            {
                context.Request.Headers["X-Integration-Partner-Id"] = authResult.Context.PartnerId;
            }

            if (authResult.Context.Permissions.Count > 0)
            {
                context.Request.Headers["X-Integration-Permissions"] = string.Join(",", authResult.Context.Permissions);
            }

            context.Request.Headers["X-Integration-Context"] = "integration_key";
        }

        if (!service.Equals("integration", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Headers.Remove("X-Integration-Key");
        }
    }
    else
    {
        if (!(context.User?.Identity?.IsAuthenticated ?? false))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        var authz = context.RequestServices.GetRequiredService<ProxyAuthorizationService>();
        var (allowed, permissionId, authError) = await authz.AuthorizeProxyRequestAsync(context, service, catchAll, context.RequestAborted);
        if (!allowed)
        {
            context.Response.StatusCode = authError switch
            {
                "unauthorized" => StatusCodes.Status401Unauthorized,
                "access_unavailable" => StatusCodes.Status503ServiceUnavailable,
                "access_error" => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status403Forbidden
            };

            await context.Response.WriteAsJsonAsync(new
            {
                error = authError ?? "forbidden",
                permission = permissionId
            });
            return;
        }
    }

    var forwarder = context.RequestServices.GetRequiredService<IHttpForwarder>();
    var requestConfig = new ForwarderRequestConfig
    {
        ActivityTimeout = TimeSpan.FromSeconds(100)
    };

    var invoker = service.Equals("documents", StringComparison.OrdinalIgnoreCase) ? documentsHttpClient : httpClient;
    var forwarderError = await forwarder.SendAsync(context, destinationPrefix, invoker, requestConfig, proxyTransformer);
    if (forwarderError != ForwarderError.None)
    {
        var errorFeature = context.GetForwarderErrorFeature();
        var exception = errorFeature?.Exception;
        app.Logger.LogWarning(exception, "Proxying to {Service} failed with {Error}", service, forwarderError);
    }
}).RequireRateLimiting("public");

// ─── SignalR Hub Proxy (/hubs/{service}) ────────────────────────────────────
// WebSocket connections to upstream SignalR hubs. Requires JWT auth.
if (upstreams.TryGetValue("hunter", out var hunterBase))
{
    app.Map("/hubs/hunter/{**rest}", async context =>
    {
        if (!(context.User?.Identity?.IsAuthenticated ?? false))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        var forwarder = context.RequestServices.GetRequiredService<IHttpForwarder>();
        var requestConfig = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromMinutes(10) // WebSocket keep-alive
        };

        var forwarderError = await forwarder.SendAsync(context, hunterBase, httpClient, requestConfig, proxyTransformer);
        if (forwarderError != ForwarderError.None)
        {
            var errorFeature = context.GetForwarderErrorFeature();
            app.Logger.LogWarning(errorFeature?.Exception, "SignalR proxy to hunter failed: {Error}", forwarderError);
        }
    });

    // Also proxy the bare /hubs/hunter (no trailing path) for negotiate
    app.Map("/hubs/hunter", async context =>
    {
        if (!(context.User?.Identity?.IsAuthenticated ?? false))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        var forwarder = context.RequestServices.GetRequiredService<IHttpForwarder>();
        var requestConfig = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromMinutes(10)
        };

        var forwarderError = await forwarder.SendAsync(context, hunterBase, httpClient, requestConfig, proxyTransformer);
        if (forwarderError != ForwarderError.None)
        {
            var errorFeature = context.GetForwarderErrorFeature();
            app.Logger.LogWarning(errorFeature?.Exception, "SignalR proxy to hunter failed: {Error}", forwarderError);
        }
    });
}

app.Run();

static Dictionary<string, string> BuildUpstreams(IConfiguration configuration)
{
    var upstreams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var child in configuration.GetSection("Gateway:Upstreams").GetChildren())
    {
        var key = child.Key?.Trim();
        var address = child.GetValue<string>("BaseAddress") ?? child.Value;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(address))
            continue;

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            continue;
        }

        upstreams[key] = uri.ToString().TrimEnd('/');
    }

    return upstreams;
}

static X509Certificate2 LoadProvidedCertificate(IConfiguration configuration)
{
    var path = configuration["Gateway:Tls:CertificatePath"]
               ?? configuration["Gateway__Tls__CertificatePath"];

    if (string.IsNullOrWhiteSpace(path))
        throw new InvalidOperationException("Gateway TLS mode 'Provided' requires Gateway:Tls:CertificatePath.");

    var password = configuration["Gateway:Tls:CertificatePassword"]
                   ?? configuration["Gateway__Tls__CertificatePassword"];

    return X509CertificateLoader.LoadPkcs12FromFile(
        path,
        password,
        X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
}

static X509Certificate2 CreateSelfSignedLocalhostCertificate()
{
    using var rsa = RSA.Create(2048);

    var req = new CertificateRequest(
        "CN=localhost",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    var san = new SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost");
    san.AddDnsName("kernel-gateway");
    san.AddIpAddress(IPAddress.Loopback);
    san.AddIpAddress(IPAddress.IPv6Loopback);

    req.CertificateExtensions.Add(san.Build());
    req.CertificateExtensions.Add(new X509KeyUsageExtension(
        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
        critical: false));

    req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
        critical: false));

    var cert = req.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddYears(1));

    return cert;
}

static HttpMessageInvoker CreateProxyHttpClient(X509Certificate2? clientCertificate)
{
    var handler = new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false
    };

    if (clientCertificate != null)
    {
        handler.SslOptions.ClientCertificates = new X509Certificate2Collection { clientCertificate };
    }

    return new HttpMessageInvoker(handler);
}

static X509Certificate2? LoadDocumentsClientCertificate(IConfiguration configuration)
{
    var path = configuration["Gateway:Documents:ClientCertificatePath"]
               ?? configuration["Gateway__Documents__ClientCertificatePath"];

    if (string.IsNullOrWhiteSpace(path))
        return null;

    var password = configuration["Gateway:Documents:ClientCertificatePassword"]
                   ?? configuration["Gateway__Documents__ClientCertificatePassword"];

    return X509CertificateLoader.LoadPkcs12FromFile(
        path,
        password,
        X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
}
