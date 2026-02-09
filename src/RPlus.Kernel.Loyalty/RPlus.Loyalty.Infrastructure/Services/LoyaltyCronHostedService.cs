using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Infrastructure.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Infrastructure.Services;

public sealed class LoyaltyCronHostedService : BackgroundService
{
    private readonly ITenureLevelRecalculator _recalculator;
    private readonly IOptionsMonitor<LoyaltyCronOptions> _options;
    private readonly ILogger<LoyaltyCronHostedService> _logger;

    public LoyaltyCronHostedService(
        ITenureLevelRecalculator recalc,
        IOptionsMonitor<LoyaltyCronOptions> options,
        ILogger<LoyaltyCronHostedService> logger)
    {
        _recalculator = recalc;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Loyalty cron is disabled ({Section}:Enabled=false).", LoyaltyCronOptions.SectionName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _recalculator.RecalculateAsync(
                    new TenureRecalcRequest(),
                    stoppingToken);

                if (result.Skipped)
                {
                    _logger.LogDebug("Tenure recalculation skipped (already up-to-date).");
                }
                else if (!result.Success)
                {
                    _logger.LogWarning("Tenure recalculation failed: {Error}", result.Error);
                }
                else
                {
                    _logger.LogInformation("Tenure recalculation completed: {Updated}/{Total}.", result.UpdatedUsers, result.TotalUsers);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenure cron loop failed.");
            }

            var delaySeconds = Math.Max(1, _options.CurrentValue.IntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }
}
