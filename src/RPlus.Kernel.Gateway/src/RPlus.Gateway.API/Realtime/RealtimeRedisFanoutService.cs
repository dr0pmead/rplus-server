using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.SDK.Gateway.Realtime;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public sealed class RealtimeRedisFanoutService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptionsMonitor<RealtimeGatewayOptions> _options;
    private readonly IRealtimeEventHub _hub;
    private readonly ILogger<RealtimeRedisFanoutService> _logger;

    public RealtimeRedisFanoutService(
        IConnectionMultiplexer redis,
        IOptionsMonitor<RealtimeGatewayOptions> options,
        IRealtimeEventHub hub,
        ILogger<RealtimeRedisFanoutService> logger)
    {
        _redis = redis;
        _options = options;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
            return;

        var channel = _options.CurrentValue.FanoutChannel;
        if (string.IsNullOrWhiteSpace(channel))
            return;

        var subscriber = _redis.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal(channel));

        queue.OnMessage(message =>
        {
            try
            {
                var delivery = JsonSerializer.Deserialize<RealtimeDeliveryMessage>(message.Message.ToString(), JsonOptions);
                if (delivery == null || string.IsNullOrWhiteSpace(delivery.UserId) || delivery.Event == null)
                    return;

                _ = _hub.PublishToUserAsync(delivery.UserId, delivery.Event, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process realtime fanout message");
            }
        });

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        finally
        {
            try
            {
                await subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
            }
            catch
            {
                // ignore
            }
        }
    }
}
