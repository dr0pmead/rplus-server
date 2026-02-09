using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Application.Handlers;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Infrastructure.Options;
using RPlus.Loyalty.Persistence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class LoyaltySchedulerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<LoyaltySchedulerOptions> _options;
    private readonly ILogger<LoyaltySchedulerHostedService> _logger;

    public LoyaltySchedulerHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<LoyaltySchedulerOptions> options,
        ILogger<LoyaltySchedulerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Loyalty scheduler is disabled ({Section}:Enabled=false).", LoyaltySchedulerOptions.SectionName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected scheduler loop error.");
            }

            var delay = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollSeconds));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        var now = DateTime.UtcNow;
        var lockUntil = now.AddSeconds(Math.Max(5, opts.LockSeconds));
        var lockedBy = Environment.MachineName;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var due = await db.ScheduledJobs
            .Where(j => j.Status == "Pending" && j.RunAtUtc <= now && (j.LockedUntilUtc == null || j.LockedUntilUtc < now))
            .OrderBy(j => j.RunAtUtc)
            .Take(Math.Max(1, opts.BatchSize))
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var job in due)
        {
            ct.ThrowIfCancellationRequested();

            var updated = await db.ScheduledJobs
                .Where(j => j.Id == job.Id && j.Status == "Pending" && (j.LockedUntilUtc == null || j.LockedUntilUtc < now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Processing")
                    .SetProperty(x => x.LockedUntilUtc, lockUntil)
                    .SetProperty(x => x.LockedBy, lockedBy)
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
                    ct);

            if (updated == 0)
            {
                continue;
            }

            try
            {
                var result = await mediator.Send(new ProcessLoyaltyScheduledJobCommand(job.Id), ct);
                _logger.LogInformation("Processed scheduled job {JobId} success={Success} points={Points}", job.Id, result.Success, result.PointsDelta);
            }
            catch (Exception ex)
            {
                await db.ScheduledJobs
                    .Where(j => j.Id == job.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, "Failed")
                        .SetProperty(x => x.LastError, ex.Message)
                        .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
                        ct);

                _logger.LogError(ex, "Failed processing scheduled job {JobId}", job.Id);
            }
        }
    }
}
