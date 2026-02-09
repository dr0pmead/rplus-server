using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Runtime.API.Services;
using RPlus.Kernel.Runtime.Application.Graph;
using RPlus.Kernel.Runtime.Application.Services;
using RPlus.Kernel.Runtime.Persistence;

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("runtime");

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // 5020: HTTP/1.1 (optional controllers)
    // 5021: HTTP/2 (gRPC, h2c)
    options.ListenAnyIP(5020, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5021, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["Runtime:ConnectionString"]
    ?? "Host=localhost;Database=runtime;Username=postgres;Password=postgres";

builder.Services.AddDbContext<RuntimeDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddSingleton<RuntimeGraphExecutor>();
builder.Services.AddScoped<RuntimeGraphExecutionService>();

builder.Services.AddHostedService<RuntimeDbInitializer>();

var app = builder.Build();

app.MapGrpcService<RuntimeGrpcService>();
app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.Run();
