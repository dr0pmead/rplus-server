using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Contracts.Domain.Loyalty;
using RPlus.SDK.Eventing;
using RPlusGrpc.Wallet;

namespace RPlus.WalletAdapter.Consumers;

/// <summary>
/// Bridges loyalty accrual requests into the core wallet service via gRPC.
/// </summary>
public sealed class LoyaltyEventsConsumer : IConsumer<EventEnvelope<LoyaltyPointsAccrualRequested_v1>>
{
    private readonly WalletService.WalletServiceClient _walletClient;
    private readonly ILogger<LoyaltyEventsConsumer> _logger;
    private readonly string _hmacSecret;

    public LoyaltyEventsConsumer(
        WalletService.WalletServiceClient walletClient,
        IConfiguration configuration,
        ILogger<LoyaltyEventsConsumer> logger)
    {
        _walletClient = walletClient;
        _logger = logger;
        _hmacSecret = configuration["Wallet:HmacSecret"] ?? "super-secret-env-key";
    }

    public async Task Consume(ConsumeContext<EventEnvelope<LoyaltyPointsAccrualRequested_v1>> context)
    {
        var envelope = context.Message;
        var payload = envelope.Payload;

        if (payload == null)
        {
            _logger.LogWarning("Loyalty event {EventId} missing payload", envelope.EventId);
            return;
        }

        var timestamp = NormalizeTimestamp(envelope.OccurredAt);
        var timestampMs = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();
        var amount = Convert.ToInt64(Math.Round(payload.Amount, MidpointRounding.AwayFromZero));
        var requestId = envelope.EventId.ToString();

        var request = new AccruePointsRequest
        {
            UserId = payload.UserId,
            Amount = amount,
            OperationId = payload.OperationId,
            Source = string.IsNullOrWhiteSpace(envelope.Source) ? "rplus.loyalty" : envelope.Source,
            Description = "Loyalty accrual",
            Timestamp = Timestamp.FromDateTime(timestamp),
            RequestId = requestId,
            Signature = GenerateSignature(payload.UserId, amount, payload.OperationId, timestampMs, requestId)
        };

        // propagate metadata for observability
        request.Metadata["eventId"] = envelope.EventId.ToString();
        request.Metadata["traceId"] = envelope.TraceId.ToString();
        foreach (var (key, value) in envelope.Metadata ?? Enumerable.Empty<KeyValuePair<string, string>>())
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Metadata[key] = value;
            }
        }

        try
        {
            _logger.LogInformation(
                "Accruing {Amount} pts for user {UserId} from loyalty event {OperationId}",
                amount,
                payload.UserId,
                payload.OperationId);

            var response = await _walletClient.AccruePointsAsync(request, cancellationToken: context.CancellationToken);

            if (!string.Equals(response.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !response.Status.Contains("Idempotent", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Wallet accrual for {UserId}/{OperationId} finished with status {Status} (code {Code})",
                    payload.UserId,
                    payload.OperationId,
                    response.Status,
                    response.ErrorCode);
            }
        }
        catch (RpcException rpcEx) when (rpcEx.StatusCode == Grpc.Core.StatusCode.Cancelled)
        {
            _logger.LogWarning("Loyalty accrual cancelled for {UserId}/{OperationId}", payload.UserId, payload.OperationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward loyalty accrual for {UserId}/{OperationId}", payload.UserId, payload.OperationId);
            throw;
        }
    }

    private static DateTime NormalizeTimestamp(DateTime occurredAt)
    {
        return occurredAt.Kind == DateTimeKind.Utc ? occurredAt : occurredAt.ToUniversalTime();
    }

    private string GenerateSignature(string userId, long amount, string operationId, long timestampMs, string requestId)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_hmacSecret));
        var payload = $"{userId}|{amount}|{operationId}|{timestampMs}|{requestId}";
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
