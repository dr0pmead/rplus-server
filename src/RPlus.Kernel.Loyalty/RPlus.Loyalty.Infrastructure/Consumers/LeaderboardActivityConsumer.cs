using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.SDK.Loyalty.Events;

namespace RPlus.Loyalty.Infrastructure.Consumers;

/// <summary>
/// Kafka consumer for activity completion events.
/// Updates Redis leaderboards in real-time.
/// </summary>
public sealed class LeaderboardActivityConsumer : KafkaConsumerBackgroundService<string, ActivityCompletedEvent>
{
    private readonly IServiceProvider _serviceProvider;

    public LeaderboardActivityConsumer(
        IOptions<KafkaOptions> options,
        IServiceProvider serviceProvider,
        ILogger<LeaderboardActivityConsumer> logger)
        : base(options, logger, LoyaltyEventTopics.ActivityCompleted)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleMessageAsync(string key, ActivityCompletedEvent message, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var leaderboard = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LeaderboardActivityConsumer>>();

        try
        {
            await leaderboard.IncrementScoreAsync(
                message.UserId,
                message.Points,
                message.CompletedAt.Year,
                message.CompletedAt.Month,
                cancellationToken);

            logger.LogInformation(
                "Activity {ActivityType} completed for user {UserId}: +{Points} points",
                message.ActivityType, message.UserId, message.Points);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update leaderboard for user {UserId}", message.UserId);
            throw; // Let Kafka handle retry
        }
    }
}
