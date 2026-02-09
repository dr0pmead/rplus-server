using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RPlus.SDK.Gateway.Realtime;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed class InMemoryRealtimeEventHub : IRealtimeEventHub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<RealtimeEventMessage>>> _userStreams = new(StringComparer.Ordinal);

    public RealtimeSubscription Subscribe(string userId, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<RealtimeEventMessage>(new BoundedChannelOptions(128)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        var streams = _userStreams.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<RealtimeEventMessage>>());
        var id = Guid.NewGuid();
        streams.TryAdd(id, channel);

        return new RealtimeSubscription(channel.Reader, () =>
        {
            if (_userStreams.TryGetValue(userId, out var userChannels))
            {
                userChannels.TryRemove(id, out _);
                if (userChannels.IsEmpty)
                    _userStreams.TryRemove(userId, out _);
            }

            channel.Writer.TryComplete();
        });
    }

    public ValueTask PublishToUserAsync(string userId, RealtimeEventMessage message, CancellationToken ct)
    {
        if (!_userStreams.TryGetValue(userId, out var channels) || channels.IsEmpty)
            return ValueTask.CompletedTask;

        foreach (KeyValuePair<Guid, Channel<RealtimeEventMessage>> kv in channels)
        {
            _ = kv.Value.Writer.TryWrite(message);
        }

        return ValueTask.CompletedTask;
    }
}
