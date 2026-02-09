using RPlus.SDK.Eventing;
using RPlus.SDK.Gateway.Realtime;
using System.Text.Json;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public interface IRealtimeEventMapper
{
    bool TryMap(EventEnvelope<JsonElement> sourceEvent, out RealtimeMappingDefinition mapping);
}
