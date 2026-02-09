using Microsoft.Extensions.Options;
using RPlus.SDK.Gateway.Realtime;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed class RedisRealtimeFanoutPublisher : IRealtimeFanoutPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptionsMonitor<RealtimeGatewayOptions> _options;

    public RedisRealtimeFanoutPublisher(IConnectionMultiplexer redis, IOptionsMonitor<RealtimeGatewayOptions> options)
    {
        _redis = redis;
        _options = options;
    }

    public async Task PublishAsync(RealtimeDeliveryMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(_options.CurrentValue.FanoutChannel), json);
    }
}
