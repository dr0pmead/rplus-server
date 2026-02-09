using System;
using System.Text.Json.Serialization;
using RPlus.SDK.Gateway.Realtime;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed record RealtimeDeliveryMessage(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("event")] RealtimeEventMessage Event
);
