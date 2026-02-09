using Confluent.Kafka;
using Microsoft.Extensions.Options;
using RPlus.Core.Options;
using System.Text.Json;

namespace RPlus.Hunter.API.Services;

/// <summary>
/// Publishes Hunter domain events to Kafka.
/// Follows the IEventPublisher pattern from service_patterns.md.
/// </summary>
public sealed class KafkaEventPublisher : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(T @event, string topic, string? aggregateId = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(@event, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        try
        {
            await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = aggregateId ?? Guid.NewGuid().ToString(),
                Value = json
            }, ct);

            _logger.LogDebug("Published event to {Topic}: {Key}", topic, aggregateId);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish event to {Topic}: {Key}", topic, aggregateId);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
