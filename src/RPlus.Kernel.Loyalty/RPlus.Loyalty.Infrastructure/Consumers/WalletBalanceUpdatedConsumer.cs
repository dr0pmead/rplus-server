using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Loyalty.Infrastructure.Services;
using RPlus.SDK.Wallet.Events;

namespace RPlus.Loyalty.Infrastructure.Consumers;

/// <summary>
/// Kafka consumer for wallet balance updates.
/// Syncs balance to Redis cache for fast access.
/// </summary>
public sealed class WalletBalanceUpdatedConsumer : KafkaConsumerBackgroundService<string, string>
{
    private readonly IWalletBalanceCache _balanceCache;
    private readonly ILogger<WalletBalanceUpdatedConsumer> _logger;

    public WalletBalanceUpdatedConsumer(
        IOptions<KafkaOptions> options,
        ILogger<WalletBalanceUpdatedConsumer> logger,
        IWalletBalanceCache balanceCache)
        : base(options, logger, WalletEventTopics.BalanceChanged)
    {
        _balanceCache = balanceCache;
        _logger = logger;
    }

    protected override async Task HandleMessageAsync(string key, string value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        try
        {
            var evt = JsonSerializer.Deserialize<WalletBalanceUpdatedDto>(value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (evt == null || string.IsNullOrWhiteSpace(evt.UserId))
            {
                _logger.LogWarning("Invalid WalletBalanceUpdated event payload");
                return;
            }

            if (!Guid.TryParse(evt.UserId, out var userId))
            {
                _logger.LogWarning("Invalid UserId in WalletBalanceUpdated: {UserId}", evt.UserId);
                return;
            }

            await _balanceCache.SetBalanceAsync(userId, evt.NewBalance, ct);
            
            _logger.LogInformation(
                "Synced wallet balance for {UserId}: {Balance}",
                userId, evt.NewBalance);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize WalletBalanceUpdated event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WalletBalanceUpdated event");
            throw; // Re-throw to trigger retry/DLQ
        }
    }

    private sealed record WalletBalanceUpdatedDto
    {
        public string UserId { get; init; } = string.Empty;
        public long PreviousBalance { get; init; }
        public long NewBalance { get; init; }
        public long ChangeAmount { get; init; }
        public string Reason { get; init; } = string.Empty;
    }
}
