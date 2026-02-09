using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Kernel.Integration.Infrastructure.Services;
using RPlus.SDK.Users.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Infrastructure.Consumers;

/// <summary>
/// Consumes HrProfileUpdatedEvent from Kafka to update scan cache user info.
/// This enables proactive caching - FIO/avatar is updated when it changes, not when scanned.
/// </summary>
public sealed class HrProfileUpdatedConsumer : KafkaConsumerBackgroundService<string, HrProfileUpdatedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HrProfileUpdatedConsumer> _logger;

    public HrProfileUpdatedConsumer(
        IOptions<KafkaOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<HrProfileUpdatedConsumer> logger)
        : base(options, logger, HrEventTopics.ProfileUpdated)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task HandleMessageAsync(
        string key,
        HrProfileUpdatedEvent message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IScanProfileCache>();

        _logger.LogDebug(
            "Updating scan cache user info for {UserId}: {FirstName} {LastName}",
            message.UserId,
            message.FirstName,
            message.LastName);

        await cache.PatchUserInfoAsync(
            message.UserId,
            message.FirstName,
            message.LastName,
            message.MiddleName,
            message.AvatarUrl,
            cancellationToken);

        _logger.LogInformation(
            "Scan cache updated for user {UserId}, name={FirstName} {LastName}",
            message.UserId,
            message.FirstName,
            message.LastName);
    }
}
