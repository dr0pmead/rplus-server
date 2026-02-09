using MassTransit;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Eventing;

namespace RPlus.WalletAdapter.Consumers;

/// <summary>
/// Observes user-domain events so that wallet telemetry can be enriched
/// (and to keep a hook for future onboarding bonuses).
/// For now we simply trace the event to keep the adapter reactive.
/// </summary>
public sealed class UserEventsConsumer : IConsumer<EventEnvelope<UserCreated>>
{
    private readonly ILogger<UserEventsConsumer> _logger;

    public UserEventsConsumer(ILogger<UserEventsConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventEnvelope<UserCreated>> context)
    {
        var envelope = context.Message;
        if (envelope.Payload is null)
        {
            _logger.LogWarning("Received user event {EventId} without payload", envelope.EventId);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Observed user event {EventType} for UserId={UserId}, Trace={TraceId}",
            envelope.EventType,
            envelope.Payload.UserId,
            envelope.TraceId);

        return Task.CompletedTask;
    }
}
