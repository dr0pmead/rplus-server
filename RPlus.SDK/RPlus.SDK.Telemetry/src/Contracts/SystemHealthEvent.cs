using RPlus.SDK.Contracts;

namespace RPlus.SDK.Telemetry.Contracts;

public record SystemHealthEvent : IntegrationEvent
{
    public SystemHealthEvent()
    {
        EventType = "telemetry.system.health";
    }

    public string Component { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string Source { get; init; } = string.Empty;
}

