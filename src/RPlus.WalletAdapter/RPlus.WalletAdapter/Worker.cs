using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RPlus.WalletAdapter;

/// <summary>
/// Placeholder background worker to keep the worker template intact.
/// The adapter logic runs via MassTransit in Program.cs, so this worker
/// simply logs a heartbeat if it ever gets registered.
/// </summary>
internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RPlus Wallet Adapter worker initialized at {Timestamp}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
