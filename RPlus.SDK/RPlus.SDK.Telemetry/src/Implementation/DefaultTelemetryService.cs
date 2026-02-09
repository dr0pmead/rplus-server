using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPlus.SDK.Telemetry.Abstractions;
using RPlus.SDK.Telemetry.Contracts;
using RPlus.SDK.Audit.Events;

namespace RPlus.SDK.Telemetry.Implementation;

public class DefaultTelemetryService : ITelemetryService
{
    private readonly ITelemetryPublisher _publisher;
    private readonly string _source;

    public DefaultTelemetryService(ITelemetryPublisher publisher)
    {
        _publisher = publisher;
        _source = AppDomain.CurrentDomain.FriendlyName;
    }

    public async Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
    {
        var @event = new TelemetryTechnicalEvent
        {
            EventName = eventName,
            Properties = properties ?? new Dictionary<string, string>(),
            Source = _source,
            Timestamp = DateTime.UtcNow
        };

        await _publisher.PublishTechnicalEventAsync(@event, "telemetry.technical");
    }

    public async Task TrackSystemHealthAsync(string component, string status, string? message = null)
    {
        var @event = new SystemHealthEvent
        {
            Component = component,
            Status = status,
            Message = message,
            Source = _source,
            Timestamp = DateTime.UtcNow
        };

        await _publisher.PublishTechnicalEventAsync(@event, "telemetry.health");
    }

    public async Task TrackTechnicalAuditAsync(string action, string resource, Dictionary<string, object>? metadata = null)
    {
        var auditPayload = new AuditEventPayload
        {
            EventId = Guid.NewGuid(),
            Source = RPlus.SDK.Audit.Enums.EventSource.Kernel,
            EventType = RPlus.SDK.Audit.Enums.AuditEventType.Technical,
            Severity = RPlus.SDK.Audit.Enums.EventSeverity.Information,
            Actor = "System",
            Action = action,
            Resource = resource,
            Metadata = metadata,
            Timestamp = DateTime.UtcNow
        };

        await _publisher.PublishAuditAsync(auditPayload);
    }
}
