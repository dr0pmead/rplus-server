using System.Text.Json.Serialization;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RealtimeRecipientStrategy
{
    None = 0,
    Metadata = 1,
    AggregateId = 2,
    Resolver = 3
}

public sealed record RealtimeRecipientsDefinition
{
    [JsonPropertyName("strategy")]
    public RealtimeRecipientStrategy Strategy { get; init; } = RealtimeRecipientStrategy.None;

    [JsonPropertyName("metadataKey")]
    public string? MetadataKey { get; init; }

    [JsonPropertyName("resolver")]
    public string? Resolver { get; init; }
}
