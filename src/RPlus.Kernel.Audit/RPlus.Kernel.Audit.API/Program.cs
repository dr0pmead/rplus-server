using RPlus.Kernel.Audit.Api.Services;
using RPlus.Kernel.Audit.Application;
using RPlus.Kernel.Audit.Infrastructure;
using RPlus.Kernel.Audit.Infrastructure.Persistence;
using RPlus.Kernel.Infrastructure.Extensions;
using RPlus.SDK.Infrastructure.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("audit");

// gRPC over http (h2c) for internal service-to-service calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // Split ports to avoid Kestrel disabling HTTP/2 when HTTP/1.1 is also enabled without TLS.
    // - 5011: HTTP/1.1 (controllers)
    // - 5010: HTTP/2 (gRPC, h2c inside docker network)
    options.ListenAnyIP(5011, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5010, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.AddAuditApplication();
builder.Services.AddAuditInfrastructure(builder.Configuration);
builder.Services.AddKernelServiceDefaults(builder.Configuration);
builder.Services.AddGrpc();

var app = builder.Build();

// Ensure schema without relying on migrations snapshot
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseKernelServiceDefaults();
app.MapControllers();
app.MapGrpcService<AuditGrpcService>();

app.Run();
