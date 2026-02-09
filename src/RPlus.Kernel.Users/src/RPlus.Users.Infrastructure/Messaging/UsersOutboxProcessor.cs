// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.Messaging.UsersOutboxProcessor
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Core.Kafka;
using RPlus.Users.Domain.Entities;
using RPlus.Users.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Users.Infrastructure.Messaging;

public class UsersOutboxProcessor : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<UsersOutboxProcessor> _logger;

  public UsersOutboxProcessor(
    IServiceProvider serviceProvider,
    ILogger<UsersOutboxProcessor> logger)
  {
    this._serviceProvider = serviceProvider;
    this._logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    this._logger.LogInformation("Users Outbox Processor is starting.");
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await this.ProcessOutboxMessagesAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        this._logger.LogError(ex, "Error processing outbox messages.");
      }
      await Task.Delay(TimeSpan.FromSeconds(5L), stoppingToken);
    }
  }

  private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
  {
    IServiceScope scope = this._serviceProvider.CreateScope();
    try
    {
      UsersDbContext dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
      IKafkaProducer<string, string> producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer<string, string>>();
      DateTime now = DateTime.UtcNow;
      string processingStatus = "Processing";
      string pendingStatus = "Pending";
      _ = await dbContext.OutboxMessages
          .Where(m => m.Status == processingStatus && m.LockedUntil < now)
          .ExecuteUpdateAsync(setters => setters
              .SetProperty(m => m.Status, pendingStatus)
              .SetProperty(m => m.LockedBy, (string?)null)
              .SetProperty(m => m.LockedUntil, (DateTime?)null), ct);
      DateTime dateTime = now.AddMinutes(5.0);
      string machineName = Environment.MachineName;
      string sql = "\n            UPDATE \"users\".\"OutboxMessages\"\n            SET \"Status\" = {0}, \"ProcessedAt\" = {1}, \"LockedBy\" = {2}, \"LockedUntil\" = {3}\n            WHERE \"Id\" IN (\n                SELECT \"Id\"\n                FROM \"users\".\"OutboxMessages\"\n                WHERE (\"Status\" = {4})\n                   OR (\"Status\" = {5} AND \"RetryCount\" < \"MaxRetries\" AND \"NextRetryAt\" <= {6})\n                   OR (\"Status\" = {7} AND \"LockedUntil\" < {8})\n                ORDER BY \"CreatedAt\"\n                LIMIT {9}\n                FOR UPDATE SKIP LOCKED\n            )\n            RETURNING \"Id\", \"AggregateId\", \"EventType\", \"Payload\", \"Topic\", \"CreatedAt\", \"Status\", \"RetryCount\", \"MaxRetries\", \"NextRetryAt\", \"ProcessedAt\", \"Error\", \"LockedBy\", \"LockedUntil\";\n        ";
      List<OutboxMessageEntity> listAsync = await dbContext.OutboxMessages.FromSqlRaw<OutboxMessageEntity>(sql, (object) processingStatus, (object) now, (object) machineName, (object) dateTime, (object) pendingStatus, (object) "Failed", (object) now, (object) processingStatus, (object) now, (object) 20).ToListAsync<OutboxMessageEntity>(ct);
      if (listAsync.Count == 0)
      {
        return;
      }

      foreach (OutboxMessageEntity message in listAsync)
      {
        try
        {
          await producer.ProduceAsync(message.Topic, message.AggregateId.ToString(), message.Payload);
          message.Status = "Processed";
          message.ProcessedAt = new DateTime?(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
          this._logger.LogError(ex, "Failed to process outbox message {Id}", (object) message.Id);
          message.Status = "Failed";
          ++message.RetryCount;
          message.Error = ex.Message;
          message.NextRetryAt = new DateTime?(DateTime.UtcNow.AddMinutes(Math.Pow(2.0, (double) message.RetryCount)));
        }
      }

      await dbContext.SaveChangesAsync(ct);
    }
    finally
    {
      scope?.Dispose();
    }
  }
}
