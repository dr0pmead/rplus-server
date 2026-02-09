using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Eventing.SchemaRegistry;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.SDK.Infrastructure.SchemaRegistry;

public sealed class EventSchemaRegistryPublisher : IEventSchemaPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly IEventPublisher _kafka;
    private readonly IOptionsMonitor<EventSchemaRegistryOptions> _options;
    private readonly ILogger<EventSchemaRegistryPublisher> _logger;
    private readonly IEventSchemaSource[] _sources;

    public EventSchemaRegistryPublisher(
        IConnectionMultiplexer redis,
        IEventPublisher kafka,
        IOptionsMonitor<EventSchemaRegistryOptions> options,
        ILogger<EventSchemaRegistryPublisher> logger,
        IEnumerable<IEventSchemaSource> sources)
    {
        _redis = redis;
        _kafka = kafka;
        _options = options;
        _logger = logger;
        _sources = sources.ToArray();
    }

    public async Task PublishAllAsync(CancellationToken ct = default)
    {
        var schemas = _sources
            .SelectMany(s => s.GetSchemas())
            .Where(s => !string.IsNullOrWhiteSpace(s.EventType) && !string.IsNullOrWhiteSpace(s.Topic))
            .GroupBy(s => s.EventType, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (schemas.Count == 0)
        {
            _logger.LogInformation("No event schemas to publish");
            return;
        }

        var opts = _options.CurrentValue;
        var db = _redis.GetDatabase();

        foreach (var schema in schemas)
        {
            ct.ThrowIfCancellationRequested();

            var json = JsonSerializer.Serialize(schema, SerializerOptions);

            // Redis cache
            await db.HashSetAsync(opts.RedisHashKey, schema.EventType, json).ConfigureAwait(false);
            await db.PublishAsync(RedisChannel.Literal(opts.RedisUpdatesChannel), schema.EventType).ConfigureAwait(false);

            // Kafka source-of-truth (compacted topic; key = eventType)
            await _kafka.PublishRawAsync(opts.KafkaTopic, json, aggregateId: schema.EventType, cancellationToken: ct)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("Published {Count} event schemas to registry", schemas.Count);
    }
}

public sealed class EventSchemaPublisherHostedService : IHostedService
{
    private readonly IEventSchemaPublisher _publisher;
    private readonly ILogger<EventSchemaPublisherHostedService> _logger;

    public EventSchemaPublisherHostedService(IEventSchemaPublisher publisher, ILogger<EventSchemaPublisherHostedService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Registry publishing must not prevent the service from starting.
            _logger.LogError(ex, "Failed to publish event schemas on startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
