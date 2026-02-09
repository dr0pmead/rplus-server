// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.AuthOutboxProcessor
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Core.Kafka;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class AuthOutboxProcessor : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly IKafkaProducer<string, string> _producer;
  private readonly ILogger<AuthOutboxProcessor> _logger;

  public AuthOutboxProcessor(
    IServiceProvider serviceProvider,
    IKafkaProducer<string, string> producer,
    ILogger<AuthOutboxProcessor> logger)
  {
    this._serviceProvider = serviceProvider;
    this._producer = producer;
    this._logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    this._logger.LogInformation("AuthOutboxProcessor is starting.");
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await this.ProcessMessagesAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        this._logger.LogError(ex, "Error occurred while processing outbox messages.");
      }
      await Task.Delay(TimeSpan.FromSeconds(5L), stoppingToken);
    }
    this._logger.LogInformation("AuthOutboxProcessor is stopping.");
  }

  private async Task ProcessMessagesAsync(CancellationToken ct)
  {
    using (IServiceScope scope = this._serviceProvider.CreateScope())
    {
      IOutboxRepository repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
      List<OutboxMessageEntity> outboxMessageEntityList = await repository.ClaimMessagesAsync(100, ct);
      if (outboxMessageEntityList.Count == 0)
        return;
      this._logger.LogDebug("Processing {Count} messages from outbox.", (object) outboxMessageEntityList.Count);
      foreach (OutboxMessageEntity message in outboxMessageEntityList)
      {
        try
        {
          await this._producer.ProduceAsync(message.Topic, message.AggregateId, message.Payload);
          await repository.MarkAsSentAsync(message.Id, ct);
          this._logger.LogDebug("Sent outbox message {Id} to topic {Topic}.", (object) message.Id, (object) message.Topic);
        }
        catch (Exception ex)
        {
          this._logger.LogWarning(ex, "Failed to send outbox message {Id} to Kafka.", (object) message.Id);
          await repository.MarkAsFailedAsync(message.Id, ex.Message, ex.StackTrace, ct);
        }
      }
    }
  }
}
