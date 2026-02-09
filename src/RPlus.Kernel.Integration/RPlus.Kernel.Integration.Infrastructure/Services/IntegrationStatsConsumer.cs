using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using RPlus.SDK.Contracts.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationStatsConsumer : KafkaConsumerBackgroundService<string, IntegrationStatsEvent>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IntegrationStatsConsumer> _logger;

    public IntegrationStatsConsumer(
        IOptions<KafkaOptions> options,
        ILogger<IntegrationStatsConsumer> logger,
        IServiceProvider serviceProvider)
        : base(options, logger, "kernel.integration.stats.v1")
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task HandleMessageAsync(
        string key,
        IntegrationStatsEvent message,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

            var entry = new IntegrationStatsEntry
            {
                PartnerId = message.PartnerId,
                KeyId = message.KeyId,
                Env = message.Env ?? "unknown",
                Scope = message.Scope ?? "unknown",
                Endpoint = message.Endpoint ?? string.Empty,
                StatusCode = message.Status,
                LatencyMs = message.LatencyMs,
                CorrelationId = message.CorrelationId ?? string.Empty,
                ErrorCode = (int)message.ErrorCode,
                CreatedAt = DateTime.UtcNow
            };

            db.Stats.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist integration stats event");
        }
    }
}
