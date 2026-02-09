using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using RPlus.SDK.Eventing.Abstractions;

namespace RPlus.SDK.Infrastructure.Services;

public class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<string, string> _producer;

    public KafkaEventPublisher(IProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task PublishAsync<T>(T @event, string eventName, string? aggregateId = null, CancellationToken cancellationToken = default) where T : class
    {
        var payload = JsonSerializer.Serialize(@event);
        var key = aggregateId ?? Guid.NewGuid().ToString();
        
        await _producer.ProduceAsync(eventName, new Message<string, string> { Key = key, Value = payload }, cancellationToken);
    }

    public async Task PublishRawAsync(string eventName, string payloadJson, string? aggregateId = null, CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(aggregateId) ? Guid.NewGuid().ToString() : aggregateId;

        await _producer.ProduceAsync(eventName, new Message<string, string> { Key = key, Value = payloadJson }, cancellationToken);
    }
}
