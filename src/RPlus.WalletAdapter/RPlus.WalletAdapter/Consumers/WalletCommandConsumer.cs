using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RPlus.WalletAdapter.Models;
using RPlusGrpc.Wallet;
using Google.Protobuf.WellKnownTypes;

namespace RPlus.WalletAdapter.Consumers;

public class WalletCommandConsumer : IConsumer<WalletCommandEvent>
{
    private readonly ILogger<WalletCommandConsumer> _logger;
    private readonly WalletService.WalletServiceClient _walletClient;
    private readonly ITopicProducer<WalletResultEvent> _resultProducer;
    private readonly string _hmacSecret;

    public WalletCommandConsumer(
        ILogger<WalletCommandConsumer> logger,
        WalletService.WalletServiceClient walletClient,
        ITopicProducer<WalletResultEvent> resultProducer,
        IConfiguration configuration)
    {
        _logger = logger;
        _walletClient = walletClient;
        _resultProducer = resultProducer;
        _hmacSecret = configuration["Wallet:HmacSecret"] ?? "super-secret-env-key";
    }

    public async Task Consume(ConsumeContext<WalletCommandEvent> context)
    {
        var cmd = context.Message;
        _logger.LogInformation("Processing Wallet Command: {Command} for User {UserId}", cmd.Context.Command, cmd.Context.UserId);

        try
        {
            var result = await DispatchCommand(cmd);
            
            var resultEvent = new WalletResultEvent
            {
                EventType = result.Success ? "wallet.command.executed" : "wallet.command.failed",
                CorrelationId = cmd.CorrelationId,
                Context = new WalletResultContext
                {
                    Status = result.Status,
                    ErrorCode = result.ErrorCode,
                    BalanceAfter = result.NewBalance,
                    OperationId = cmd.Context.OperationId
                }
            };

            await _resultProducer.Produce(resultEvent);
            _logger.LogInformation("Command {Command} result published. Status: {Status}", cmd.Context.Command, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Wallet Command {Command}", cmd.Context.Command);
            // Optional: Publish critical failure event
        }
    }

    private async Task<(bool Success, string Status, long NewBalance, string? ErrorCode)> DispatchCommand(WalletCommandEvent cmd)
    {
        var ctx = cmd.Context;
        var now = DateTimeOffset.UtcNow;
        var timestampProto = Timestamp.FromDateTime(now.UtcDateTime);
        
        // Generate Signature for Wallet gRPC
        var payload = $"{ctx.UserId}|{ctx.Amount}|{ctx.OperationId}|{now.ToUnixTimeMilliseconds()}|{ctx.RequestId}";
        var signature = GenerateSignature(payload);

        switch (ctx.Command.ToLower())
        {
            case "accrue":
                var request = new AccruePointsRequest
                {
                    UserId = ctx.UserId,
                    Amount = ctx.Amount,
                    OperationId = ctx.OperationId,
                    Source = cmd.Source.Service,
                    Description = ctx.Metadata.TryGetValue("description", out var d) ? d.ToString() : "Command-driven accrual",
                    Timestamp = timestampProto,
                    RequestId = ctx.RequestId,
                    Signature = signature
                };
                CopyMetadata(ctx.Metadata, request);

                var resp = await _walletClient.AccruePointsAsync(request);
                return (resp.Status == "Completed" || resp.Status.Contains("Idempotent"), resp.Status, resp.BalanceAfter, resp.ErrorCode);

            // Add other cases (reserve, commit, cancel) as needed
            default:
                return (false, "Failed", 0, "UNKNOWN_COMMAND");
        }
    }

    private static void CopyMetadata(Dictionary<string, object> metadata, AccruePointsRequest request)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return;
        }

        foreach (var kvp in metadata)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                continue;
            }

            var value = kvp.Value?.ToString();
            if (value != null)
            {
                request.Metadata[kvp.Key] = value;
            }
        }
    }

    private string GenerateSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLower();
    }
}
