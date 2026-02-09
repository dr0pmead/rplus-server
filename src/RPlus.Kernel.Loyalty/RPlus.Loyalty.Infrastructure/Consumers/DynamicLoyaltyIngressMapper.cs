using RPlus.SDK.Eventing.SchemaRegistry;
using RPlus.SDK.Loyalty.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RPlus.Loyalty.Infrastructure.Consumers;

public static class DynamicLoyaltyIngressMapper
{
    public static LoyaltyTriggerEvent? TryMap(
        IReadOnlyList<EventSchemaDescriptor> candidatesForTopic,
        string topic,
        string key,
        string valueJson,
        bool includeRawPayload)
    {
        if (candidatesForTopic.Count == 0)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(valueJson);
        var root = doc.RootElement;

        var envelopeEventType = TryExtractString(root, "EventType");
        var schema = SelectSchema(candidatesForTopic, envelopeEventType);
        if (schema == null)
        {
            return null;
        }

        var userId = TryExtractString(root, schema.Hints.SubjectIdPath)
                     ?? TryExtractString(root, "Payload.UserId")
                     ?? TryExtractString(root, "UserId");

        if (!Guid.TryParse(userId, out var userGuid) || userGuid == Guid.Empty)
        {
            return null;
        }

        var occurredAt = TryExtractOccurredAt(root, schema.Hints.OccurredAtPath)
                         ?? TryExtractOccurredAt(root, "OccurredAt")
                         ?? DateTime.UtcNow;

        var operationId = TryExtractString(root, schema.Hints.OperationIdPath)
                          ?? TryExtractString(root, "EventId")
                          ?? ComputeDeterministicId(topic, key, valueJson);

        var source = TryExtractString(root, "Source");
        if (string.IsNullOrWhiteSpace(source))
        {
            source = string.IsNullOrWhiteSpace(schema.ProducerService) ? topic : schema.ProducerService;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (metaKey, path) in schema.Hints.Metadata)
        {
            var metaValue = TryExtractString(root, path);
            if (!string.IsNullOrWhiteSpace(metaKey) && !string.IsNullOrWhiteSpace(metaValue))
            {
                metadata[metaKey] = metaValue!;
            }
        }

        return new LoyaltyTriggerEvent
        {
            EventType = string.IsNullOrWhiteSpace(envelopeEventType) ? schema.EventType : envelopeEventType,
            UserId = userGuid,
            OperationId = operationId,
            Metadata = metadata,
            Payload = includeRawPayload ? valueJson : null,
            Source = source,
            OccurredAt = occurredAt
        };
    }

    private static EventSchemaDescriptor? SelectSchema(IReadOnlyList<EventSchemaDescriptor> candidates, string? envelopeEventType)
    {
        if (!string.IsNullOrWhiteSpace(envelopeEventType))
        {
            return candidates.FirstOrDefault(c => string.Equals(c.EventType, envelopeEventType, StringComparison.OrdinalIgnoreCase));
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static DateTime? TryExtractOccurredAt(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!TryExtractElement(root, path, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unixMs))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
        }

        return null;
    }

    private static string ComputeDeterministicId(string topic, string key, string valueJson)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{topic}:{key}:{valueJson}"));
        return Convert.ToHexString(bytes);
    }

    private static string? TryExtractString(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!TryExtractElement(root, path, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static bool TryExtractElement(JsonElement root, string path, out JsonElement value)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (!TryGetPropertyCaseInsensitive(current, segment, out var next))
            {
                value = default;
                return false;
            }

            current = next;
        }

        value = current;
        return true;
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

