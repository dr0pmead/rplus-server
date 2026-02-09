using System.Text.Json.Serialization;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public sealed record RealtimeEventDescriptor(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("version")] int Version
);
