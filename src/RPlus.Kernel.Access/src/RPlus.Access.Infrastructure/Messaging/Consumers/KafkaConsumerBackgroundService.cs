// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Messaging.Consumers.KafkaConsumerBackgroundService`1
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Options;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Messaging.Consumers;

public abstract class KafkaConsumerBackgroundService<TMessage> : BackgroundService
{
  protected readonly ILogger _logger;
  private readonly string _topic;
  private readonly string _groupId;
  private readonly ConsumerConfig _config;

  protected KafkaConsumerBackgroundService(
    IOptions<KafkaOptions> options,
    ILogger logger,
    string topic,
    string groupId)
  {
    this._logger = logger;
    this._topic = topic;
    this._groupId = groupId;
    ConsumerConfig consumerConfig = new ConsumerConfig();
    consumerConfig.BootstrapServers = options.Value.BootstrapServers;
    consumerConfig.GroupId = groupId;
    consumerConfig.AutoOffsetReset = new AutoOffsetReset?(AutoOffsetReset.Earliest);
    consumerConfig.EnableAutoCommit = new bool?(false);
    this._config = consumerConfig;
    if (!string.IsNullOrEmpty(this._config.BootstrapServers))
      return;
    this._config.BootstrapServers = Environment.GetEnvironmentVariable("ConnectionStrings__Kafka") ?? "kafka:9092";
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using (IConsumer<string, string> consumer = new ConsumerBuilder<string, string>((IEnumerable<KeyValuePair<string, string>>) this._config).Build())
    {
      consumer.Subscribe(this._topic);
      this._logger.LogInformation("Kafka Consumer started. Group: {GroupId}, Topic: {Topic}", (object) this._groupId, (object) this._topic);
      try
      {
        while (!stoppingToken.IsCancellationRequested)
        {
          try
          {
            ConsumeResult<string, string> result = consumer.Consume(stoppingToken);
            if (result != null)
            {
              TMessage message = JsonSerializer.Deserialize<TMessage>(result.Message.Value, new JsonSerializerOptions()
              {
                PropertyNameCaseInsensitive = true
              });
              if ((object) message != null)
              {
                await this.HandleAsync(message, stoppingToken);
                consumer.Commit(result);
              }
            }
            result = (ConsumeResult<string, string>) null;
          }
          catch (OperationCanceledException ex)
          {
            break;
          }
          catch (Exception ex)
          {
            this._logger.LogError(ex, "Error consuming Kafka message");
            await Task.Delay(1000, stoppingToken);
          }
        }
      }
      finally
      {
        consumer.Close();
      }
    }
  }

  protected abstract Task HandleAsync(TMessage message, CancellationToken ct);
}
