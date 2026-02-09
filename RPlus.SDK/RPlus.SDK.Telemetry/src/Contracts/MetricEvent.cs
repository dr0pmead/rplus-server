using System;
using System.Collections.Generic;
using RPlus.SDK.Contracts;

namespace RPlus.SDK.Telemetry.Contracts;

public enum MetricType
{
    Counter = 1,
    Gauge = 2,
    Histogram = 3,
    Duration = 4
}

public record MetricEvent : IntegrationEvent
{
    public MetricEvent()
    {
        EventType = "telemetry.metric.recorded";
    }

    public Guid EventId { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public double Value { get; init; }
    public MetricType Type { get; init; }
    public string? Unit { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
    public string Source { get; init; } = string.Empty;
    public string? Actor { get; init; }
    public string? Resource { get; init; }
    public string NodeId { get; init; } = string.Empty;
}
