using Microsoft.Extensions.Options;
using RPlus.SDK.Eventing;
using RPlus.SDK.Gateway.Realtime;
using System;
using System.Text.Json;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed class RealtimeEventMapper : IRealtimeEventMapper
{
    private readonly IOptionsMonitor<RealtimeGatewayOptions> _options;

    public RealtimeEventMapper(IOptionsMonitor<RealtimeGatewayOptions> options)
    {
        _options = options;
    }

    public bool TryMap(EventEnvelope<JsonElement> sourceEvent, out RealtimeMappingDefinition mapping)
    {
        mapping = default!;
        if (sourceEvent == null) return false;

        var eventType = sourceEvent.EventType?.Trim();
        if (string.IsNullOrWhiteSpace(eventType)) return false;

        var mappings = _options.CurrentValue.Mappings;
        if (mappings.Count == 0) return false;

        return mappings.TryGetValue(eventType, out mapping!);
    }
}
