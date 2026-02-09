using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Domain.Entities;
using RPlus.SDK.Audit.Events;

namespace RPlus.Kernel.Audit.Infrastructure.Messaging;

public sealed class AuditEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditEventConsumer> _logger;
    private readonly ConsumerConfig _consumerConfig;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public AuditEventConsumer(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<AuditEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var kafkaSection = configuration.GetSection("Kafka");
        var bootstrapServers = kafkaSection["BootstrapServers"] ?? configuration.GetConnectionString("Kafka") ?? "kernel-kafka:9092";
        var groupId = kafkaSection["AuditConsumerGroup"] ?? "rplus-audit-service";

        _consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();
        consumer.Subscribe(AuditEventTopics.KernelAuditEvents);

        _logger.LogInformation("Audit event consumer subscribed to {Topic}", AuditEventTopics.KernelAuditEvents);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message?.Value == null)
                    {
                        continue;
                    }

                    await HandleMessageAsync(result.Message.Value, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming audit event");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleMessageAsync(string payload, CancellationToken cancellationToken)
    {
        AuditEventPayload? message;
        try
        {
            message = JsonSerializer.Deserialize<AuditEventPayload>(payload, _serializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize audit event payload");
            return;
        }

        if (message == null)
        {
            _logger.LogWarning("Received empty audit event payload");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditRepository>();

        if (await repository.ExistsAsync(message.EventId))
        {
            _logger.LogDebug("Audit event {EventId} already stored. Skipping.", message.EventId);
            return;
        }

        var auditEvent = AuditEvent.FromExternal(
            message.EventId,
            AuditEnumMapper.ToDomainSource(message.Source),
            AuditEnumMapper.ToDomainEventType(message.EventType),
            AuditEnumMapper.ToDomainSeverity(message.Severity),
            message.Actor,
            message.Action,
            message.Resource,
            message.Metadata ?? new(),
            message.Timestamp,
            message.PreviousEventHash,
            message.Signature,
            message.SignerId);

        await repository.AddAsync(auditEvent);
        _logger.LogInformation("Persisted audit event {EventId} from Kafka", message.EventId);
    }
}
