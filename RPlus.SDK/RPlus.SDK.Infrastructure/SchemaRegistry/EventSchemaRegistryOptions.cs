namespace RPlus.SDK.Infrastructure.SchemaRegistry;

public sealed class EventSchemaRegistryOptions
{
    public const string SectionName = "SchemaRegistry";

    public string KafkaTopic { get; set; } = "system.event.schemas.v1";

    public string RedisHashKey { get; set; } = "rplus:schema:events";

    public string RedisUpdatesChannel { get; set; } = "rplus:schema:events:updated";
}

