using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Options;
using RPlus.Loyalty.Application.Handlers;
using RPlus.Loyalty.Infrastructure.Options;
using RPlus.SDK.Eventing.SchemaRegistry;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using RPlus.SDK.Loyalty.Events;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Infrastructure.Consumers;

public sealed class LoyaltyDynamicIngressConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventSchemaRegistryReader _registry;
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptions<KafkaOptions> _kafkaOptions;
    private readonly IOptionsMonitor<LoyaltyDynamicConsumptionOptions> _options;
    private readonly IOptionsMonitor<EventSchemaRegistryOptions> _registryOptions;
    private readonly ILogger<LoyaltyDynamicIngressConsumer> _logger;

    private volatile bool _reloadRequested;

    public LoyaltyDynamicIngressConsumer(
        IServiceProvider serviceProvider,
        IEventSchemaRegistryReader registry,
        IConnectionMultiplexer redis,
        IOptions<KafkaOptions> kafkaOptions,
        IOptionsMonitor<LoyaltyDynamicConsumptionOptions> options,
        IOptionsMonitor<EventSchemaRegistryOptions> registryOptions,
        ILogger<LoyaltyDynamicIngressConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _registry = registry;
        _redis = redis;
        _kafkaOptions = kafkaOptions;
        _options = options;
        _registryOptions = registryOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _logger.LogInformation("Loyalty dynamic ingress is disabled ({Section}:Enabled=false).", LoyaltyDynamicConsumptionOptions.SectionName);
            return;
        }

        TrySubscribeToRegistryUpdates();

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.Value.BootstrapServers,
            GroupId = opts.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = _kafkaOptions.Value.SessionTimeoutSeconds.HasValue ? _kafkaOptions.Value.SessionTimeoutSeconds.Value * 1000 : null
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        var currentTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schemasByTopic = new Dictionary<string, List<EventSchemaDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var lastRefresh = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (_reloadRequested || now - lastRefresh >= TimeSpan.FromSeconds(Math.Max(1, opts.RefreshIntervalSeconds)))
                {
                    _reloadRequested = false;
                    lastRefresh = now;
                    await RefreshSubscriptionsAsync(consumer, currentTopics, schemasByTopic, stoppingToken);
                }

                if (currentTopics.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, opts.EmptyRegistryDelaySeconds)), stoppingToken);
                    continue;
                }

                var result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                if (result == null)
                {
                    continue;
                }

                await HandleMessageAsync(schemasByTopic, result, opts.IncludeRawPayload, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error (reason: {Reason})", ex.Error.Reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in loyalty dynamic ingress consumer.");
            }
        }

        consumer.Close();
    }

    private void TrySubscribeToRegistryUpdates()
    {
        try
        {
            var channel = new RedisChannel(_registryOptions.CurrentValue.RedisUpdatesChannel, RedisChannel.PatternMode.Literal);
            _redis.GetSubscriber().Subscribe(channel, (_, _) => _reloadRequested = true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe to schema registry Redis updates channel. Falling back to polling.");
        }
    }

    private async Task RefreshSubscriptionsAsync(
        IConsumer<string, string> consumer,
        HashSet<string> currentTopics,
        Dictionary<string, List<EventSchemaDescriptor>> schemasByTopic,
        CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        IReadOnlyList<EventSchemaDescriptor> schemas;

        try
        {
            schemas = await _registry.GetAllAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read schema registry from Redis; keeping previous topic subscription.");
            return;
        }

        var allowPrefixes = opts.TopicPrefixesAllowlist ?? [];
        bool IsAllowed(string topic)
        {
            if (allowPrefixes.Length == 0)
            {
                return true;
            }

            return allowPrefixes.Any(p => !string.IsNullOrWhiteSpace(p) && topic.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        var nextByTopic = schemas
            .Where(s => !string.IsNullOrWhiteSpace(s.Topic))
            .Where(s => IsAllowed(s.Topic))
            .GroupBy(s => s.Topic.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var nextTopics = nextByTopic.Keys
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (nextTopics.Length == 0)
        {
            if (currentTopics.Count > 0)
            {
                _logger.LogWarning("Schema registry returned 0 allowed topics; unsubscribing loyalty dynamic ingress consumer.");
                currentTopics.Clear();
                schemasByTopic.Clear();
                consumer.Unsubscribe();
            }

            return;
        }

        var currentOrdered = currentTopics.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray();
        var changed = currentOrdered.Length != nextTopics.Length || !currentOrdered.SequenceEqual(nextTopics, StringComparer.OrdinalIgnoreCase);

        schemasByTopic.Clear();
        foreach (var kvp in nextByTopic)
        {
            schemasByTopic[kvp.Key] = kvp.Value;
        }

        if (!changed)
        {
            return;
        }

        currentTopics.Clear();
        foreach (var t in nextTopics)
        {
            currentTopics.Add(t);
        }

        consumer.Subscribe(nextTopics);
        _logger.LogInformation("Loyalty dynamic ingress subscribed to {TopicCount} topic(s).", nextTopics.Length);
    }

    private async Task HandleMessageAsync(
        IReadOnlyDictionary<string, List<EventSchemaDescriptor>> schemasByTopic,
        ConsumeResult<string, string> result,
        bool includeRawPayload,
        CancellationToken ct)
    {
        if (!schemasByTopic.TryGetValue(result.Topic, out var candidates) || candidates.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(result.Message?.Value))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var mode = _options.CurrentValue.ProcessingMode?.Trim() ?? "graphs";
        if (mode.Equals("triggers", StringComparison.OrdinalIgnoreCase))
        {
            LoyaltyTriggerEvent? trigger;
            try
            {
                trigger = DynamicLoyaltyIngressMapper.TryMap(
                    candidates,
                    result.Topic,
                    result.Message.Key ?? string.Empty,
                    result.Message.Value,
                    includeRawPayload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map Kafka message (topic: {Topic}, partition: {Partition}, offset: {Offset}). Skipping.", result.Topic, result.Partition.Value, result.Offset.Value);
                return;
            }

            if (trigger == null)
            {
                return;
            }

            await mediator.Send(new ProcessLoyaltyEventCommand(trigger), ct);
            return;
        }

        await mediator.Send(new ProcessLoyaltyIngressEventCommand(
            Topic: result.Topic,
            Key: result.Message.Key ?? string.Empty,
            ValueJson: result.Message.Value,
            SchemasForTopic: candidates), ct);
    }
}
