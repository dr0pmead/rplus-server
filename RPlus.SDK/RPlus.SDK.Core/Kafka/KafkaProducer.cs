using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Core.Kafka;

public class KafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>, IDisposable
{
    private readonly IProducer<TKey, TValue> _producer;
    private readonly ILogger<KafkaProducer<TKey, TValue>> _logger;

    public KafkaProducer(IOptions<ProducerConfig> config, ILogger<KafkaProducer<TKey, TValue>> logger)
    {
        _logger = logger;
        // Basic serializer configuration - assuming string/json or compatible standard serializers
        // For generic TKey/TValue we might need specific serializers if not standard types.
        // For now, using default builders which handle primitives and some standard types.
        // In a real generic implementation, we might need ISchemaRegistry or similar, 
        // but looking at usage (Loyalty/Auth), keys are likely strings/Guids and values are classes.
        // Confluent.Kafka defaults might fail for complex types without explicit serializers.
        // However, I will use a builder and rely on Newtonsoft/System.Text.Json if needed or default behavior.
        // Given 'RPlus.SDK.Core', let's assume JSON serialization for values.
        
        var builder = new ProducerBuilder<TKey, TValue>(config.Value);
        
        // If TKey or TValue are not primitives, we need serializers.
        // Assuming simple usage or that Confluent can handle it or that we register global serializers (not visible here).
        // Let's implement a safe JSON serializer approach if needed, but for now standard builder.
        // Actually, to be safe and generic:
        if (typeof(TValue).IsClass && typeof(TValue) != typeof(string))
        {
            builder.SetValueSerializer(new KafkaJsonSerializer<TValue>());
        }
        if (typeof(TKey).IsClass && typeof(TKey) != typeof(string))
        {
            builder.SetKeySerializer(new KafkaJsonSerializer<TKey>());
        }

        _producer = builder.Build();
    }

    public async Task ProduceAsync(string topic, TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        try
        {
            await _producer.ProduceAsync(topic, new Message<TKey, TValue> { Key = key, Value = value }, cancellationToken);
        }
        catch (ProduceException<TKey, TValue> ex)
        {
            _logger.LogError(ex, "Failed to produce message to topic {Topic}", topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}

// Simple JSON Serializer helper
public class KafkaJsonSerializer<T> : ISerializer<T>
{
    public byte[] Serialize(T data, SerializationContext context)
    {
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data);
    }
}
