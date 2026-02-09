using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Persistence;

namespace RPlus.Kernel.Integration.Infrastructure.Services;

/// <summary>
/// Background service that expires stale pending scans.
/// Runs hourly to prevent table bloat from abandoned orders.
/// </summary>
public sealed class ScanCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScanCleanupService> _logger;

    /// <summary>
    /// Cleanup interval (1 hour).
    /// </summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Age threshold for expiring pending scans (24 hours).
    /// </summary>
    private static readonly TimeSpan ExpirationThreshold = TimeSpan.FromHours(24);

    public ScanCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ScanCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScanCleanupService started, running every {Interval}", CleanupInterval);

        using var timer = new PeriodicTimer(CleanupInterval);

        // Initial delay to avoid startup congestion
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DoCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scan cleanup");
            }
        }

        _logger.LogInformation("ScanCleanupService stopped");
    }

    private async Task DoCleanupAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(ExpirationThreshold);

        // EF Core 7+ ExecuteUpdateAsync for bulk update (no entity loading)
        var expiredCount = await db.PartnerScans
            .Where(s => s.Status == PartnerScanStatus.Pending && s.CreatedAt < cutoff)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, PartnerScanStatus.Expired),
                ct);

        if (expiredCount > 0)
        {
            _logger.LogInformation(
                "Expired {Count} stale scans older than {Threshold}",
                expiredCount, ExpirationThreshold);
        }
        else
        {
            _logger.LogDebug("No stale scans to expire");
        }
    }
}
