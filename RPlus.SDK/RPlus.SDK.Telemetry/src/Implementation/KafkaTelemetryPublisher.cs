using System.Threading.Tasks;
using RPlus.SDK.Telemetry.Abstractions;
using RPlus.SDK.Telemetry.Contracts;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Audit.Events;

namespace RPlus.SDK.Telemetry.Implementation;

public class KafkaTelemetryPublisher : ITelemetryPublisher
{
    private readonly IEventPublisher _eventPublisher;

    public KafkaTelemetryPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task PublishMetricAsync(MetricEvent @event)
    {
        await _eventPublisher.PublishAsync(@event, "telemetry.metrics");
    }

    public async Task PublishTechnicalEventAsync(object @event, string topic)
    {
        await _eventPublisher.PublishAsync(@event, topic);
    }

    public async Task PublishAuditAsync(AuditEventPayload payload)
    {
        await _eventPublisher.PublishAsync(payload, "kernel.audit.events");
    }
}
