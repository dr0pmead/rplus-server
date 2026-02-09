using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Users.Application.Interfaces.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Users.Infrastructure.Consumers;

public sealed record IntegrationScanContextEvent(
    Guid IntegrationId,
    string ContextId,
    string UserId,
    string EventType,
    string RequestId,
    DateTime CreatedAt)
{
    public const string EventName = "integration.scan.context.v1";
}

public sealed class IntegrationScanContextConsumer : KafkaConsumer<IntegrationScanContextEvent>
{
    private readonly IServiceProvider _serviceProvider;

    public IntegrationScanContextConsumer(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> options,
        ILogger<IntegrationScanContextConsumer> logger)
        : base(options, IntegrationScanContextEvent.EventName, "rplus-users-qr-refresh", logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleAsync(IntegrationScanContextEvent message, CancellationToken ct)
    {
        if (!Guid.TryParse(message.UserId, out var userId))
        {
            _logger.LogWarning("Invalid user id in {EventType}: {UserId}", IntegrationScanContextEvent.EventName, message.UserId);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var qr = scope.ServiceProvider.GetRequiredService<IUserQrService>();
        await qr.IssueAsync(userId, message.RequestId, ct);
    }
}
