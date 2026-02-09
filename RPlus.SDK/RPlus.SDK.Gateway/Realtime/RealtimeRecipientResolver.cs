using RPlus.SDK.Eventing;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public static class RealtimeRecipientResolver
{
    public static IReadOnlyCollection<string> ResolveRecipients(
        EventEnvelope<JsonElement> sourceEvent,
        RealtimeRecipientsDefinition recipients)
    {
        if (recipients.Strategy == RealtimeRecipientStrategy.None)
            return [];

        if (recipients.Strategy == RealtimeRecipientStrategy.Metadata)
        {
            var key = recipients.MetadataKey?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                return [];

            if (sourceEvent.Metadata == null || sourceEvent.Metadata.Count == 0)
                return [];

            if (!sourceEvent.Metadata.TryGetValue(key, out var value))
                return [];

            if (string.IsNullOrWhiteSpace(value))
                return [];

            return [value.Trim()];
        }

        if (recipients.Strategy == RealtimeRecipientStrategy.AggregateId)
        {
            var id = sourceEvent.AggregateId?.Trim();
            if (string.IsNullOrWhiteSpace(id))
                return [];

            return [id];
        }

        // Resolver strategy is reserved for future extension; never implicit.
        return [];
    }
}
