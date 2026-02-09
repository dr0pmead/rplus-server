// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Messaging.IdempotentConsumer`1
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces.Monitoring;
using RPlus.Core.Kafka;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Messaging;

public abstract class IdempotentConsumer<TMessage> : IKafkaConsumer<TMessage> where TMessage : class
{
  private readonly IConnectionMultiplexer _redis;
  protected readonly ILogger _logger;
  protected readonly IAccessMetrics? _metrics;
  private readonly TimeSpan _dedupTtl = TimeSpan.FromDays(7);

  protected IdempotentConsumer(
    IConnectionMultiplexer redis,
    ILogger logger,
    IAccessMetrics? metrics = null)
  {
    this._redis = redis;
    this._logger = logger;
    this._metrics = metrics;
  }

  public async Task ConsumeAsync(TMessage message, CancellationToken ct)
  {
    string eventId = this.GetDedupKey(message);
    if (string.IsNullOrEmpty(eventId))
    {
      this._logger.LogWarning("Event of type {Type} has no EventId. Processing without idempotency check.", (object) typeof (TMessage).Name);
      await this.HandleAsync(message, ct);
      return;
    }

    IDatabase db = this._redis.GetDatabase();
    string key = "processed_events:" + eventId;
    if (await db.KeyExistsAsync(key))
    {
      this._logger.LogInformation("Event {EventId} already processed. Skipping.", (object) eventId);
      this._metrics?.IncEventConsumed(typeof (TMessage).Name, "duplicate");
      return;
    }

    try
    {
      await this.HandleAsync(message, ct);
      await db.StringSetAsync(key, DateTime.UtcNow.ToString("O"), this._dedupTtl);
      this._metrics?.IncEventConsumed(typeof (TMessage).Name, "success");
    }
    catch (Exception ex)
    {
      this._metrics?.IncEventConsumed(typeof (TMessage).Name, "failed");
      this._logger.LogError(ex, "Error processing event {EventId}. Will retry.", (object) eventId);
      throw;
    }
  }

  protected abstract string GetDedupKey(TMessage message);

  protected abstract Task HandleAsync(TMessage message, CancellationToken ct);
}
