namespace RPlus.Core.Kafka;

using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RPlus.Core.Options;

#nullable enable

/// <summary>
/// Base class for Kafka consumers with built-in retry policy and Dead Letter Queue (DLQ) support.
/// </summary>
public abstract class KafkaConsumerBackgroundService<TKey, TValue> : BackgroundService
{
    private readonly KafkaOptions _options;
    protected readonly ILogger _logger;
    private readonly string _topic;

    // DLQ producer (lazy initialized)
    private IProducer<TKey, string>? _dlqProducer;
    private string DlqTopic => $"{_topic}_error";

    // Retry configuration: exponential backoff
    private readonly TimeSpan[] _retryDelays =
    {
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10) // Final retry - give external services time to recover
    };

    protected KafkaConsumerBackgroundService(IOptions<KafkaOptions> options, ILogger logger, string topic)
    {
        _options = options.Value;
        _logger = logger;
        _topic = topic;
    }

    protected abstract Task HandleMessageAsync(TKey key, TValue message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // CRITICAL: Manual commit only after successful processing or DLQ
            SessionTimeoutMs = _options.SessionTimeoutSeconds.HasValue ? _options.SessionTimeoutSeconds.Value * 1000 : null
        };

        using var consumer = new ConsumerBuilder<TKey, TValue>(config)
            .SetKeyDeserializer(KafkaSerialization.GetDeserializer<TKey>())
            .SetValueDeserializer(KafkaSerialization.GetDeserializer<TValue>())
            .Build();

        consumer.Subscribe(_topic);
        _logger.LogInformation("Kafka consumer started for topic {Topic} with DLQ {DlqTopic}", _topic, DlqTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
                    if (result != null)
                    {
                        var success = await HandleWithRetryPolicyAsync(result, stoppingToken);

                        if (success)
                        {
                            // Commit ONLY if successfully processed OR successfully sent to DLQ
                            consumer.Commit(result);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    // Kafka protocol errors (deserialization, etc.)
                    _logger.LogError(ex, "Kafka consume error on topic {Topic}", _topic);
                    await Task.Delay(1000, stoppingToken); // Brief pause before retry
                }
                catch (Exception ex)
                {
                    // Critical consumer infrastructure error
                    _logger.LogCritical(ex, "Fatal Kafka consumer error on topic {Topic}", _topic);
                    await Task.Delay(5000, stoppingToken); // Longer pause for critical errors
                }
            }
        }
        finally
        {
            consumer.Close();
            _dlqProducer?.Flush(TimeSpan.FromSeconds(5)); // Flush DLQ buffer
            _dlqProducer?.Dispose();
            _logger.LogInformation("Kafka consumer stopped for topic {Topic}", _topic);
        }
    }

    /// <summary>
    /// Handles message with exponential retry policy. On permanent failure, sends to DLQ.
    /// </summary>
    private async Task<bool> HandleWithRetryPolicyAsync(ConsumeResult<TKey, TValue> result, CancellationToken ct)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _retryDelays.Length; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = _retryDelays[attempt - 1];
                    _logger.LogWarning(
                        "Retry {Attempt}/{Max} for message at offset {Offset} on topic {Topic}, waiting {Delay}ms...",
                        attempt, _retryDelays.Length, result.Offset.Value, _topic, delay.TotalMilliseconds);
                    await Task.Delay(delay, ct);
                }

                await HandleMessageAsync(result.Message.Key, result.Message.Value, ct);
                return true; // Success!
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == _retryDelays.Length)
                {
                    // All retries exhausted → move to DLQ
                    _logger.LogError(ex,
                        "All {MaxRetries} retries exhausted for message at offset {Offset}. Moving to DLQ: {DlqTopic}",
                        _retryDelays.Length, result.Offset.Value, DlqTopic);

                    await SendToDlqAsync(result, ex, ct);
                    return true; // Return true to commit offset - message is now in DLQ
                }
            }
        }

        return false; // Should never reach here
    }

    /// <summary>
    /// Sends failed message to Dead Letter Queue topic.
    /// </summary>
    private async Task SendToDlqAsync(ConsumeResult<TKey, TValue> result, Exception ex, CancellationToken ct)
    {
        try
        {
            EnsureDlqProducer();

            var errorPayload = new DlqPayload<TValue>
            {
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                OriginalKey = result.Message.Key?.ToString(),
                OriginalValue = result.Message.Value,
                Topic = result.Topic,
                Partition = result.Partition.Value,
                Offset = result.Offset.Value,
                FailedAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(errorPayload, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            await _dlqProducer!.ProduceAsync(DlqTopic, new Message<TKey, string>
            {
                Key = result.Message.Key,
                Value = json
            }, ct);

            _logger.LogWarning(
                "Message sent to DLQ {DlqTopic}: offset={Offset}, partition={Partition}, error={Error}",
                DlqTopic, result.Offset.Value, result.Partition.Value, ex.Message);
        }
        catch (Exception dlqEx)
        {
            // DLQ send failed - log but don't throw (we still want to commit to prevent infinite loop)
            _logger.LogCritical(dlqEx,
                "CRITICAL: Failed to send message to DLQ {DlqTopic}. Message may be lost! Offset={Offset}",
                DlqTopic, result.Offset.Value);
        }
    }

    /// <summary>
    /// Lazy initializes the DLQ producer.
    /// </summary>
    private void EnsureDlqProducer()
    {
        if (_dlqProducer != null) return;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All // Guarantee delivery to DLQ
        };

        _dlqProducer = new ProducerBuilder<TKey, string>(config).Build();
    }

    /// <summary>
    /// Payload format for Dead Letter Queue messages.
    /// </summary>
    private sealed class DlqPayload<T>
    {
        public string? Error { get; init; }
        public string? StackTrace { get; init; }
        public string? OriginalKey { get; init; }
        public T? OriginalValue { get; init; }
        public string? Topic { get; init; }
        public int Partition { get; init; }
        public long Offset { get; init; }
        public DateTime FailedAtUtc { get; init; }
    }
}
