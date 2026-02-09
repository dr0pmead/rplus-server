using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RPlus.SDK.Gateway.Realtime;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public interface IRealtimeEventHub
{
    RealtimeSubscription Subscribe(string userId, CancellationToken ct);
    ValueTask PublishToUserAsync(string userId, RealtimeEventMessage message, CancellationToken ct);
}

public sealed class RealtimeSubscription : IDisposable
{
    private readonly Action _dispose;

    public ChannelReader<RealtimeEventMessage> Reader { get; }

    public RealtimeSubscription(ChannelReader<RealtimeEventMessage> reader, Action dispose)
    {
        Reader = reader;
        _dispose = dispose;
    }

    public void Dispose() => _dispose();
}
