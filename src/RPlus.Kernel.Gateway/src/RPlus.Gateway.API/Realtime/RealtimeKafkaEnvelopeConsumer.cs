using Microsoft.Extensions.Logging;
using RPlus.Core.Kafka;
using RPlus.SDK.Gateway.Realtime;
using RPlus.SDK.Eventing;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed class RealtimeKafkaEnvelopeConsumer : IKafkaConsumer<EventEnvelope<JsonElement>>
{
    private readonly IRealtimeEventMapper _mapper;
    private readonly IRealtimeFanoutPublisher _fanout;
    private readonly IRealtimePolicyService _policy;
    private readonly ILogger<RealtimeKafkaEnvelopeConsumer> _logger;

    public RealtimeKafkaEnvelopeConsumer(
        IRealtimeEventMapper mapper,
        IRealtimeFanoutPublisher fanout,
        IRealtimePolicyService policy,
        ILogger<RealtimeKafkaEnvelopeConsumer> logger)
    {
        _mapper = mapper;
        _fanout = fanout;
        _policy = policy;
        _logger = logger;
    }

    public async Task ConsumeAsync(EventEnvelope<JsonElement> message, CancellationToken cancellationToken)
    {
        if (!_mapper.TryMap(message, out var mapping))
            return;

        var recipients = RealtimeRecipientResolver.ResolveRecipients(message, mapping.Recipients);
        if (recipients.Count == 0)
            return;

        var realtime = new RealtimeEventMessage(
            Type: mapping.Type,
            Category: mapping.Category,
            Action: mapping.Action,
            Target: mapping.Target,
            SourceEventId: message.EventId,
            OccurredAt: message.OccurredAt,
            AggregateId: message.AggregateId);

        try
        {
            foreach (var userId in recipients)
            {
                if (!await _policy.IsAllowedAsync(userId, mapping.RequiredPermission, cancellationToken))
                    continue;

                await _fanout.PublishAsync(new RealtimeDeliveryMessage(userId, realtime), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish realtime projection for {EventType}", message.EventType);
        }
    }
}
