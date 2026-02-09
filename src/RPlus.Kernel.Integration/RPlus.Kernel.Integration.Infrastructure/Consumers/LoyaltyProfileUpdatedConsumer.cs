using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Kernel.Integration.Infrastructure.Services;
using RPlus.SDK.Loyalty.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Infrastructure.Consumers;

/// <summary>
/// Consumes LoyaltyProfileUpdatedEvent from Kafka to update scan cache discount.
/// This enables proactive caching - discount is updated when it changes, not when scanned.
/// </summary>
public sealed class LoyaltyProfileUpdatedConsumer : KafkaConsumerBackgroundService<string, LoyaltyProfileUpdatedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoyaltyProfileUpdatedConsumer> _logger;

    public LoyaltyProfileUpdatedConsumer(
        IOptions<KafkaOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<LoyaltyProfileUpdatedConsumer> logger)
        : base(options, logger, LoyaltyEventTopics.ProfileUpdated)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task HandleMessageAsync(
        string key,
        LoyaltyProfileUpdatedEvent message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IScanProfileCache>();

        _logger.LogDebug(
            "Updating scan cache for user {UserId}: Level={Level}/{Total}, RPlusDiscount={Discount}%",
            message.UserId,
            message.CurrentLevel,
            message.TotalLevels,
            message.RPlusDiscount);

        // v3.0: Use PatchV3Async for scalable discount system
        await cache.PatchV3Async(
            message.UserId,
            message.CurrentLevel,
            message.TotalLevels,
            message.RPlusDiscount,
            cancellationToken);

        _logger.LogInformation(
            "Scan cache updated for user {UserId}, Level={Level}/{Total}, RPlusDiscount={Discount}%",
            message.UserId,
            message.CurrentLevel,
            message.TotalLevels,
            message.RPlusDiscount);
    }
}
