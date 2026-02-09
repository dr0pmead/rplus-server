using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Audit.Infrastructure.Persistence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Audit.Infrastructure.Services;

public sealed class AuditRetentionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditRetentionWorker> _logger;
    private readonly IOptionsMonitor<AuditRetentionOptions> _options;

    public AuditRetentionWorker(
        IServiceProvider serviceProvider,
        ILogger<AuditRetentionWorker> logger,
        IOptionsMonitor<AuditRetentionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.CleanupIntervalHours)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var retentionDays = Math.Max(1, _options.CurrentValue.RetentionDays);
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            await db.AuditEvents
                .Where(e => e.Timestamp < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup audit events");
        }
    }
}
