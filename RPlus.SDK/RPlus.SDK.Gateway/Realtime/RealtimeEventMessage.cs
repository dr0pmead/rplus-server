using System;
using System.Text.Json.Serialization;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public sealed record RealtimeEventMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("sourceEventId")] Guid SourceEventId,
    [property: JsonPropertyName("occurredAt")] DateTime OccurredAt,
    [property: JsonPropertyName("aggregateId")] string AggregateId
);

