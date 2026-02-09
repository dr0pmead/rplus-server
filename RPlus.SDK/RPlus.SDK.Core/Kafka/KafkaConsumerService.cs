// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Kafka.KafkaConsumerService`1
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Core.Kafka;

public sealed class KafkaConsumerService<TMessage> : BackgroundService where TMessage : class?
{
  private readonly IServiceProvider _serviceProvider;
  private readonly string _bootstrapServers;
  private readonly string _topic;
  private readonly string _groupId;
  private readonly ILogger<KafkaConsumerService<TMessage>> _logger;

  public KafkaConsumerService(
    IServiceProvider serviceProvider,
    string bootstrapServers,
    string topic,
    string groupId)
  {
    this._serviceProvider = serviceProvider;
    this._bootstrapServers = bootstrapServers;
    this._topic = topic;
    this._groupId = groupId;
    this._logger = serviceProvider.GetRequiredService<ILogger<KafkaConsumerService<TMessage>>>();
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    ConsumerConfig config = new ConsumerConfig();
    config.BootstrapServers = string.IsNullOrWhiteSpace(this._bootstrapServers) ? "kernel-kafka:9092" : this._bootstrapServers;
    config.GroupId = string.IsNullOrWhiteSpace(this._groupId) ? "rplus-default" : this._groupId;
    config.AutoOffsetReset = new AutoOffsetReset?(AutoOffsetReset.Earliest);
    config.EnableAutoCommit = new bool?(false);
    using (IConsumer<string, TMessage> consumer = new ConsumerBuilder<string, TMessage>((IEnumerable<KeyValuePair<string, string>>) config).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(KafkaSerialization.GetDeserializer<TMessage>()).Build())
    {
      consumer.Subscribe(this._topic);
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          ConsumeResult<string, TMessage>? result = consumer.Consume(stoppingToken);
          if (result?.Message?.Value is TMessage value)
          {
            using var scope = this._serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IKafkaConsumer<TMessage>>();
            await handler.ConsumeAsync(value, stoppingToken);
            consumer.Commit(result!);
          }
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (ConsumeException ex)
        {
          this._logger.LogError((Exception) ex, "Kafka consume error for topic {Topic}", (object) this._topic);
        }
        catch (Exception ex)
        {
          this._logger.LogError(ex, "Kafka handler error for topic {Topic}", (object) this._topic);
        }
      }
      consumer.Close();
    }
  }
}
