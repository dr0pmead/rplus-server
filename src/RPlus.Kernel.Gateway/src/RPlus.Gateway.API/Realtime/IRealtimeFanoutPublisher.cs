using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public interface IRealtimeFanoutPublisher
{
    Task PublishAsync(RealtimeDeliveryMessage message, CancellationToken ct);
}

