using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Runtime.Persistence;

namespace RPlus.Kernel.Runtime.API.Services;

public sealed class RuntimeDbInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RuntimeDbInitializer> _logger;

    public RuntimeDbInitializer(IServiceProvider services, ILogger<RuntimeDbInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
                _logger.LogInformation("Applying Runtime schema (Attempt {Attempt})...", attempt);
                await RuntimeSchemaBootstrapper.ApplyAsync(db, cancellationToken);
                await db.Database.EnsureCreatedAsync(cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Runtime database. Retrying...");
                if (attempt == maxRetries)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
