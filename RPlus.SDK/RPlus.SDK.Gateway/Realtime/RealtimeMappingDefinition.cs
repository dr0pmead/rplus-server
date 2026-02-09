using System.Text.Json.Serialization;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public sealed record RealtimeMappingDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = "invalidation";

    [JsonPropertyName("action")]
    public string Action { get; init; } = "invalidate";

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("requiredPermission")]
    public string? RequiredPermission { get; init; }

    [JsonPropertyName("recipients")]
    public RealtimeRecipientsDefinition Recipients { get; init; } = new();

    public RealtimeEventDescriptor ToDescriptor()
        => new(Type, Category, Action, Target, Version);
}
