namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltyDynamicConsumptionOptions
{
    public const string SectionName = "Loyalty:DynamicConsumption";

    /// <summary>
    /// Enables v2 dynamic Kafka consumption based on the Schema Registry (Redis cache).
    /// When enabled, per-topic <see cref="RPlus.Loyalty.Infrastructure.Consumers.LoyaltyIngressConsumer"/> registrations should be disabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Kafka consumer group id used by the dynamic ingress consumer.
    /// Keep it distinct from the legacy v1 loyalty ingress group to avoid mixed balancing.
    /// </summary>
    public string GroupId { get; set; } = "rplus-loyalty-ingress-v2";

    /// <summary>
    /// Periodic refresh interval of topics/schemas from Redis, in seconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Delay used when the registry is empty (no topics yet), in seconds.
    /// </summary>
    public int EmptyRegistryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Optional allowlist for topics (prefix match, case-insensitive). When empty, all registry topics are accepted.
    /// Example: ["domain.", "auth.", "security."].
    /// </summary>
    public string[] TopicPrefixesAllowlist { get; set; } = [];

    /// <summary>
    /// If true, stores the raw event JSON into <c>LoyaltyTriggerEvent.Payload</c> for future v2 filtering/JsonLogic.
    /// </summary>
    public bool IncludeRawPayload { get; set; } = true;

    /// <summary>
    /// Processing mode:
    /// - "graphs": store raw ingress events and execute v2 graph rules (JsonLogic).
    /// - "triggers": legacy v1 mapping into <c>LoyaltyTriggerEvent</c> and flat rule engine.
    /// </summary>
    public string ProcessingMode { get; set; } = "graphs";
}
