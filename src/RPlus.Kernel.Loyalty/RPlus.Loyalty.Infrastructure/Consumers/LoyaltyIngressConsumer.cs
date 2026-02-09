using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Loyalty.Application.Handlers;
using RPlus.Loyalty.Infrastructure.Options;
using RPlus.SDK.Loyalty.Events;

namespace RPlus.Loyalty.Infrastructure.Consumers;

public sealed class LoyaltyIngressConsumer : KafkaConsumerBackgroundService<string, JsonElement>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _topic;
    private readonly IOptionsMonitor<LoyaltyIngressOptions> _options;

    public LoyaltyIngressConsumer(
        IOptions<KafkaOptions> kafkaOptions,
        IOptionsMonitor<LoyaltyIngressOptions> options,
        IServiceProvider serviceProvider,
        ILogger<LoyaltyIngressConsumer> logger,
        string topic)
        : base(kafkaOptions, logger, topic)
    {
        _serviceProvider = serviceProvider;
        _topic = topic;
        _options = options;
    }

    protected override async Task HandleMessageAsync(string key, JsonElement message, CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Mappings.TryGetValue(_topic, out var mapping) ||
            string.IsNullOrWhiteSpace(mapping.TriggerEventType))
        {
            return;
        }

        var userId = TryExtractString(message, mapping.UserIdPath)
                     ?? TryExtractString(message, "Payload.UserId")
                     ?? TryExtractString(message, "UserId");

        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogWarning("Loyalty ingress topic {Topic} could not extract valid UserId (path {Path})", _topic, mapping.UserIdPath);
            return;
        }

        var occurredAt = TryExtractDateTime(message, mapping.OccurredAtPath)
                         ?? TryExtractDateTime(message, "OccurredAt")
                         ?? DateTime.UtcNow;

        var operationId = TryExtractString(message, mapping.OperationIdPath)
                          ?? TryExtractString(message, "EventId")
                          ?? ComputeDeterministicId(_topic, message);

        var source = mapping.Source;
        if (string.IsNullOrWhiteSpace(source))
        {
            source = TryExtractString(message, "Source") ?? _topic;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (metaKey, path) in mapping.Metadata)
        {
            var value = TryExtractString(message, path);
            if (!string.IsNullOrWhiteSpace(metaKey) && value != null)
            {
                metadata[metaKey] = value;
            }
        }

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new ProcessLoyaltyEventCommand(new LoyaltyTriggerEvent
        {
            EventType = mapping.TriggerEventType,
            UserId = userGuid,
            OperationId = operationId,
            Metadata = metadata,
            Source = source,
            OccurredAt = occurredAt
        }), cancellationToken);
    }

    private static string ComputeDeterministicId(string topic, JsonElement message)
    {
        var raw = message.GetRawText();
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(topic + ":" + raw));
        return Convert.ToHexString(bytes);
    }

    private static DateTime? TryExtractDateTime(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var value = TryExtractString(root, path);
        if (value == null)
        {
            return null;
        }

        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
        }

        return null;
    }

    private static string? TryExtractString(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!TryGetPropertyCaseInsensitive(current, segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
