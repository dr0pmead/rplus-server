// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Services.AuditCleanupWorker
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Access.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Services;

public class AuditCleanupWorker : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<AuditCleanupWorker> _logger;
  private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
  private const int RetentionDays = 90;

  public AuditCleanupWorker(IServiceProvider serviceProvider, ILogger<AuditCleanupWorker> logger)
  {
    this._serviceProvider = serviceProvider;
    this._logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    this._logger.LogInformation("AuditCleanupWorker started. Retention: {RetentionDays} days.", (object) 90);
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await this.CleanupAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        this._logger.LogError(ex, "Error during audit cleanup cycle");
      }
      await Task.Delay(AuditCleanupWorker.Interval, stoppingToken);
    }
  }

  private async Task CleanupAsync(CancellationToken ct)
  {
    IServiceScope scope = this._serviceProvider.CreateScope();
    try
    {
      AccessDbContext requiredService = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
      DateTime dateTime = DateTime.UtcNow.AddDays(-90.0);
      this._logger.LogInformation("Retention Policy: cleaning up audit logs older than {Cutoff}", (object) dateTime);
      int num = await requiredService.Database.ExecuteSqlRawAsync("DELETE FROM access.integration_audit_logs WHERE timestamp < {0}", (IEnumerable<object>) new object[1]
      {
        (object) dateTime
      }, ct);
      if (num > 0)
      {
        this._logger.LogInformation("Retention Policy applied: Deleted {Count} old audit logs.", (object) num);
        scope = (IServiceScope) null;
      }
      else
      {
        this._logger.LogInformation("Retention Policy applied: No logs found for deletion.");
        scope = (IServiceScope) null;
      }
    }
    finally
    {
      scope?.Dispose();
    }
  }
}
