// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.IntegrationDbInitializer
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationDbInitializer : IHostedService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<IntegrationDbInitializer> _logger;

  public IntegrationDbInitializer(
    IServiceProvider serviceProvider,
    ILogger<IntegrationDbInitializer> logger)
  {
    this._serviceProvider = serviceProvider;
    this._logger = logger;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    const int maxRetries = 5;

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
      try
      {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

        _logger.LogInformation("Applying IntegrationDbContext schema (Attempt {Attempt})...", attempt);
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS integration;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS access_level text NOT NULL DEFAULT 'limited';", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS discount_partner numeric NULL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS profile_fields jsonb NOT NULL DEFAULT '[]'::jsonb;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
          "ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS metadata jsonb NOT NULL DEFAULT '{{}}'::jsonb;",
          cancellationToken);
        // Dynamic Level-Based Discount System columns
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS discount_strategy text NOT NULL DEFAULT 'dynamic_level';", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS partner_category text NOT NULL DEFAULT 'retail';", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS max_discount numeric(5,2);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS integration.partners ADD COLUMN IF NOT EXISTS happy_hours_config jsonb;", cancellationToken);
        // Data Patch: Existing partners with discount_partner should use 'fixed' strategy (backwards compatibility)
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE integration.partners 
            SET discount_strategy = 'fixed' 
            WHERE discount_partner IS NOT NULL 
              AND discount_strategy = 'dynamic_level';", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS integration.integration_stats (
  id BIGSERIAL PRIMARY KEY,
  partner_id uuid NOT NULL,
  key_id uuid NOT NULL,
  env text NOT NULL,
  scope text NOT NULL,
  endpoint text NOT NULL,
  status_code integer NOT NULL,
  latency_ms bigint NOT NULL,
  correlation_id text NOT NULL,
  error_code integer NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS integration.integration_stats_daily (
  stat_date date NOT NULL,
  partner_id uuid NOT NULL,
  key_id uuid NOT NULL,
  env text NOT NULL,
  scope text NOT NULL,
  endpoint text NOT NULL,
  status_code integer NOT NULL,
  count bigint NOT NULL,
  error_count bigint NOT NULL,
  avg_latency_ms double precision NOT NULL,
  max_latency_ms bigint NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (stat_date, partner_id, key_id, env, scope, endpoint, status_code)
);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS integration.list_sync_configs (
  id uuid PRIMARY KEY,
  integration_id uuid NOT NULL,
  list_id uuid NOT NULL,
  is_enabled boolean NOT NULL DEFAULT false,
  allow_delete boolean NOT NULL DEFAULT false,
  strict boolean NOT NULL DEFAULT false,
  mapping_json jsonb NOT NULL DEFAULT '{{}}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS integration.list_sync_runs (
  id uuid PRIMARY KEY,
  integration_id uuid NOT NULL,
  list_id uuid NOT NULL,
  api_key_id uuid NULL,
  mode text NOT NULL DEFAULT 'upsert',
  items_count integer NOT NULL DEFAULT 0,
  created_count integer NOT NULL DEFAULT 0,
  updated_count integer NOT NULL DEFAULT 0,
  deleted_count integer NOT NULL DEFAULT 0,
  failed_count integer NOT NULL DEFAULT 0,
  error_samples_json jsonb NULL,
  started_at timestamptz NOT NULL DEFAULT now(),
  finished_at timestamptz NULL,
  duration_ms bigint NULL
);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'integration'
      AND table_name = 'integration_stats'
      AND column_name = 'environment'
  ) THEN
    ALTER TABLE integration.integration_stats
      ADD COLUMN IF NOT EXISTS env text;
    UPDATE integration.integration_stats
      SET env = environment
      WHERE env IS NULL;
    ALTER TABLE integration.integration_stats
      ALTER COLUMN environment SET DEFAULT 'unknown';
    UPDATE integration.integration_stats
      SET environment = 'unknown'
      WHERE environment IS NULL;
  END IF;
END $$;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
ALTER TABLE IF EXISTS integration.integration_stats
  ALTER COLUMN env SET DEFAULT 'unknown';", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
UPDATE integration.integration_stats
  SET env = 'unknown'
  WHERE env IS NULL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(@"
ALTER TABLE IF EXISTS integration.integration_stats
  ALTER COLUMN env SET NOT NULL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_created_at ON integration.integration_stats(created_at);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_partner_id ON integration.integration_stats(partner_id);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_key_id ON integration.integration_stats(key_id);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_scope ON integration.integration_stats(scope);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_daily_date ON integration.integration_stats_daily(stat_date);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_integration_stats_daily_partner_id ON integration.integration_stats_daily(partner_id);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS ix_list_sync_configs_integration_list ON integration.list_sync_configs(integration_id, list_id);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_list_sync_runs_integration_id ON integration.list_sync_runs(integration_id);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_list_sync_runs_list_id ON integration.list_sync_runs(list_id);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS ix_list_sync_runs_started_at ON integration.list_sync_runs(started_at);", cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        return;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to initialize Integration database. Retrying...");
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
