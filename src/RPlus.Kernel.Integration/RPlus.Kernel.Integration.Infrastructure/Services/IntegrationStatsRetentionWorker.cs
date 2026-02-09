using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationStatsRetentionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IntegrationStatsRetentionWorker> _logger;
    private readonly IOptionsMonitor<IntegrationStatsRetentionOptions> _options;

    public IntegrationStatsRetentionWorker(
        IServiceProvider serviceProvider,
        ILogger<IntegrationStatsRetentionWorker> logger,
        IOptionsMonitor<IntegrationStatsRetentionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RollupIntervalHours)));
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
            var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            var options = _options.CurrentValue;

            var lookbackDays = Math.Max(1, options.RollupLookbackDays);
            var rawRetentionDays = Math.Max(1, options.RawRetentionDays);
            var dailyRetentionMonths = Math.Max(1, options.DailyRetentionMonths);

            var rollupSql = $@"
INSERT INTO integration.integration_stats_daily
(
  stat_date,
  partner_id,
  key_id,
  env,
  scope,
  endpoint,
  status_code,
  count,
  error_count,
  avg_latency_ms,
  max_latency_ms
)
SELECT
  date_trunc('day', created_at)::date AS stat_date,
  partner_id,
  key_id,
  env,
  scope,
  endpoint,
  status_code,
  COUNT(*)::bigint AS count,
  SUM(CASE WHEN status_code >= 400 THEN 1 ELSE 0 END)::bigint AS error_count,
  AVG(latency_ms)::double precision AS avg_latency_ms,
  MAX(latency_ms)::bigint AS max_latency_ms
FROM integration.integration_stats
WHERE created_at >= now() - interval '{lookbackDays} days'
GROUP BY 1,2,3,4,5,6,7
ON CONFLICT (stat_date, partner_id, key_id, env, scope, endpoint, status_code)
DO UPDATE SET
  count = EXCLUDED.count,
  error_count = EXCLUDED.error_count,
  avg_latency_ms = EXCLUDED.avg_latency_ms,
  max_latency_ms = EXCLUDED.max_latency_ms;";

            await db.Database.ExecuteSqlRawAsync(rollupSql, cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                $"DELETE FROM integration.integration_stats WHERE created_at < now() - interval '{rawRetentionDays} days';",
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                $"DELETE FROM integration.integration_stats_daily WHERE stat_date < (current_date - interval '{dailyRetentionMonths} months');",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to roll up or cleanup integration stats");
        }
    }
}
