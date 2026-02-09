using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Wallet.Commands;
using RPlus.SDK.Wallet.Events;
using RPlus.SDK.Wallet.Results;
using RPlus.Wallet.Domain.Entities;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;
using RPlus.SDK.Eventing.Abstractions;

#nullable enable
namespace RPlus.Wallet.Application.Commands.ReservePoints;

public class ReservePointsHandler : IRequestHandler<ReservePointsCommand, ReservePointsResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ReservePointsHandler> _logger;
    private readonly IEventPublisher _eventPublisher;
    private readonly string _hmacSecret;

    public ReservePointsHandler(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        IDistributedCache cache,
        ILogger<ReservePointsHandler> logger,
        IConfiguration configuration,
        IEventPublisher eventPublisher)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
        _cache = cache;
        _logger = logger;
        _eventPublisher = eventPublisher;
        _hmacSecret = configuration["Wallet:HmacSecret"] ?? "super-secret-env-key";
    }

    public async Task<ReservePointsResult> Handle(ReservePointsCommand request, CancellationToken cancellationToken)
    {
        if (!VerifySignature($"{request.UserId}|{request.Amount}|{request.OperationId}|{request.Timestamp}|{request.RequestId}", request.Signature))
        {
            return new ReservePointsResult(false, "Failed", 0, "INVALID_SIGNATURE");
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (Math.Abs(nowMs - request.Timestamp) > TimeSpan.FromMinutes(5).TotalMilliseconds)
        {
            return new ReservePointsResult(false, "Failed", 0, "TIMESTAMP_OUT_OF_WINDOW");
        }

        var replayKey = $"wallet:replay:{request.RequestId}";
        if (await _cache.GetStringAsync(replayKey, cancellationToken) != null)
        {
            return new ReservePointsResult(false, "Failed", 0, "REPLAY_DETECTED");
        }

        await _cache.SetStringAsync(
            replayKey,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            cancellationToken);

        if (await _walletRepository.GetTransactionByOperationIdAsync(request.OperationId, cancellationToken) != null)
        {
            var wallet = await _walletRepository.GetByUserIdAsync(Guid.Parse(request.UserId), cancellationToken);
            if (wallet == null)
            {
                return new ReservePointsResult(true, "Completed (Idempotent)", 0);
            }

            var available = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId) -
                            _encryptionService.DecryptLong(wallet.ReservedBalanceEncrypted, wallet.BalanceKeyId);
            return new ReservePointsResult(true, "Completed (Idempotent)", available);
        }

        await _walletRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            var userId = Guid.Parse(request.UserId);
            var wallet = await _walletRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
            if (wallet == null)
            {
                return new ReservePointsResult(false, "Failed", 0, "WALLET_NOT_FOUND");
            }

            var actualBalance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
            var reservedBalance = _encryptionService.DecryptLong(wallet.ReservedBalanceEncrypted, wallet.BalanceKeyId);
            var availableBalance = actualBalance - reservedBalance;
            if (availableBalance < request.Amount)
            {
                await _walletRepository.RollbackTransactionAsync(cancellationToken);
                return new ReservePointsResult(false, "Failed", availableBalance, "INSUFFICIENT_FUNDS");
            }

            var newReserved = reservedBalance + request.Amount;
            wallet.UpdateReserved(_encryptionService.Encrypt(newReserved), _encryptionService.GetCurrentKeyId());
            wallet.Version += 1;
            await _walletRepository.UpdateAsync(wallet, cancellationToken);

            var transaction = WalletTransaction.Create(
                userId,
                request.OperationId,
                request.RequestId,
                _encryptionService.Encrypt(request.Amount),
                _encryptionService.Encrypt(actualBalance),
                _encryptionService.Encrypt(actualBalance),
                request.Source,
                "Pending",
                _encryptionService.GetCurrentKeyId(),
                _encryptionService.Encrypt(request.Description),
                _encryptionService.Encrypt(request.Metadata));

            await _walletRepository.AddTransactionAsync(transaction, cancellationToken);
            await _walletRepository.SaveChangesAsync(cancellationToken);
            await _walletRepository.CommitTransactionAsync(cancellationToken);

            await PublishAsync(new WalletTransactionCreated(
                transaction.Id.ToString(),
                request.UserId,
                request.Amount,
                request.OperationId,
                request.Source,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), WalletEventTopics.TransactionCreated, transaction.Id.ToString(), cancellationToken);

            return new ReservePointsResult(true, "Completed", actualBalance - newReserved);
        }
        catch (Exception ex)
        {
            await _walletRepository.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Reserve error");
            return new ReservePointsResult(false, "Failed", 0, "INTERNAL_ERROR");
        }
    }

    private bool VerifySignature(string payload, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).Equals(signature, StringComparison.OrdinalIgnoreCase);
    }

    private Task PublishAsync<T>(T payload, string topic, string aggregateId, CancellationToken ct) where T : class =>
        _eventPublisher.PublishAsync(payload, topic, aggregateId, ct);
}
