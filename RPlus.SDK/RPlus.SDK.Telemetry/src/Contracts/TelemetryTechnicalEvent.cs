using System.Collections.Generic;
using RPlus.SDK.Contracts;

namespace RPlus.SDK.Telemetry.Contracts;

public record TelemetryTechnicalEvent : IntegrationEvent
{
    public TelemetryTechnicalEvent()
    {
        EventType = "telemetry.technical.event";
    }

    public string EventName { get; init; } = string.Empty;
    public Dictionary<string, string> Properties { get; init; } = new();
    public string Source { get; init; } = string.Empty;
}

