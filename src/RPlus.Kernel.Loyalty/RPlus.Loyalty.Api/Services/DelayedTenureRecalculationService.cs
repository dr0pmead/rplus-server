using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Application.Abstractions;

namespace RPlus.Loyalty.Api.Services;

/// <summary>
/// Runs tenure level recalculation 5 minutes after startup.
/// This delay allows all dependent services (HR, etc.) to become available.
/// </summary>
public sealed class DelayedTenureRecalculationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DelayedTenureRecalculationService> _logger;
    private readonly TimeSpan _startupDelay = TimeSpan.FromMinutes(5);

    public DelayedTenureRecalculationService(
        IServiceProvider serviceProvider,
        ILogger<DelayedTenureRecalculationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tenure recalculation scheduled to run in {Delay}", _startupDelay);

        try
        {
            await Task.Delay(_startupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tenure recalculation cancelled during startup delay");
            return;
        }

        _logger.LogInformation("Starting delayed tenure recalculation...");

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var recalculator = scope.ServiceProvider.GetRequiredService<ITenureLevelRecalculator>();
            
            var result = await recalculator.RecalculateAsync(
                new TenureRecalcRequest(Force: false), // Don't force - respect hash check
                stoppingToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Tenure recalculation completed: {Total} users, {Updated} updated",
                    result.TotalUsers,
                    result.UpdatedUsers);
            }
            else
            {
                _logger.LogWarning(
                    "Tenure recalculation failed: {Error}",
                    result.Error ?? "unknown");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenure recalculation failed with exception");
        }
    }
}
