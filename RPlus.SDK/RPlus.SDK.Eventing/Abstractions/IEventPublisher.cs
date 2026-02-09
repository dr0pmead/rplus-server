using System.Threading;
using System.Threading.Tasks;

namespace RPlus.SDK.Eventing.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string eventName, string? aggregateId = null, CancellationToken cancellationToken = default) where T : class;
    Task PublishRawAsync(string eventName, string payloadJson, string? aggregateId = null, CancellationToken cancellationToken = default);
}
