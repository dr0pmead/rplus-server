using System.Threading.Tasks;
using RPlus.SDK.Telemetry.Contracts;
using RPlus.SDK.Audit.Events;

namespace RPlus.SDK.Telemetry.Abstractions;

public interface ITelemetryPublisher
{
    Task PublishMetricAsync(MetricEvent @event);
    Task PublishTechnicalEventAsync(object @event, string topic);
    Task PublishAuditAsync(AuditEventPayload payload);
}
