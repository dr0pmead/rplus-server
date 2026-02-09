using System;

namespace RPlus.SDK.Contracts;

public record IntegrationEvent
{
    public string EventType { get; init; } = string.Empty;
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid? TraceId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string SourceService { get; init; } = string.Empty;
    public string Version { get; init; } = "v1";
}
