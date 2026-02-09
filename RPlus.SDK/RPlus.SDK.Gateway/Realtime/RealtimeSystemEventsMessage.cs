using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public sealed record RealtimeSystemEventsMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] RealtimeSystemEventsPayload Payload
)
{
    public static RealtimeSystemEventsMessage Create(IReadOnlyCollection<RealtimeEventDescriptor> events)
        => new("system.events", new RealtimeSystemEventsPayload(events));
}

public sealed record RealtimeSystemEventsPayload(
    [property: JsonPropertyName("events")] IReadOnlyCollection<RealtimeEventDescriptor> Events
);

