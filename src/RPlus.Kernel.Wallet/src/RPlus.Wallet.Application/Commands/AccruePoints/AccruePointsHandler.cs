using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Wallet.Commands;
using RPlus.SDK.Wallet.Events;
using RPlus.SDK.Wallet.Results;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.Wallet.Domain.Entities;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;
using WalletEntity = RPlus.Wallet.Domain.Entities.Wallet;

#nullable enable
namespace RPlus.Wallet.Application.Commands.AccruePoints;

public class AccruePointsHandler : IRequestHandler<AccruePointsCommand, AccruePointsResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AccruePointsHandler> _logger;
    private readonly string _hmacSecret;
    private readonly IEventPublisher _eventPublisher;

    public AccruePointsHandler(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        IDistributedCache cache,
        ILogger<AccruePointsHandler> logger,
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

    public async Task<AccruePointsResult> Handle(AccruePointsCommand request, CancellationToken cancellationToken)
    {
        if (!VerifySignature($"{request.UserId}|{request.Amount}|{request.OperationId}|{request.Timestamp}|{request.RequestId}", request.Signature))
        {
            _logger.LogWarning("Invalid signature for operation {OperationId}", request.OperationId);
            return new AccruePointsResult(false, "Failed", 0, "INVALID_SIGNATURE");
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Allow backdated accruals for admin testing (source = "admin_backdate")
        var allowBackdate = request.Source?.Equals("admin_backdate", StringComparison.OrdinalIgnoreCase) == true;
        if (!allowBackdate && Math.Abs(nowMs - request.Timestamp) > TimeSpan.FromMinutes(5).TotalMilliseconds)
        {
            return new AccruePointsResult(false, "Failed", 0, "TIMESTAMP_OUT_OF_WINDOW");
        }

        var replayKey = $"wallet:replay:{request.RequestId}";
        if (await _cache.GetStringAsync(replayKey, cancellationToken) != null)
        {
            return new AccruePointsResult(false, "Failed", 0, "REPLAY_DETECTED");
        }

        await _cache.SetStringAsync(
            replayKey,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            cancellationToken);

        var existing = await _walletRepository.GetTransactionByOperationIdAsync(request.OperationId, cancellationToken);
        if (existing != null)
        {
            var wallet = await _walletRepository.GetByUserIdAsync(Guid.Parse(request.UserId), cancellationToken);
            if (wallet == null)
            {
                return new AccruePointsResult(true, "Completed (Idempotent)", 0);
            }

            var balance = _encryptionService.DecryptLong(existing.BalanceAfterEncrypted, existing.KeyId);
            return new AccruePointsResult(true, "Completed (Idempotent)", balance);
        }

        await _walletRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            var userId = Guid.Parse(request.UserId);
            var wallet = await _walletRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
            if (wallet == null)
            {
                wallet = new WalletEntity(userId, _encryptionService.Encrypt(0L), _encryptionService.GetCurrentKeyId());
                await _walletRepository.AddAsync(wallet, cancellationToken);
            }

            var balance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
            var newBalance = balance + request.Amount;
            if (newBalance < 0)
            {
                await _walletRepository.RollbackTransactionAsync(cancellationToken);
                return new AccruePointsResult(false, "Failed", 0, "INSUFFICIENT_FUNDS");
            }

            var encryptedNewBalance = _encryptionService.Encrypt(newBalance);
            wallet.UpdateBalance(encryptedNewBalance, _encryptionService.GetCurrentKeyId());
            wallet.Version += 1;
            await _walletRepository.UpdateAsync(wallet, cancellationToken);

            var transaction = WalletTransaction.Create(
                userId,
                request.OperationId,
                request.RequestId,
                _encryptionService.Encrypt(request.Amount),
                _encryptionService.Encrypt(balance),
                encryptedNewBalance,
                request.Source,
                "Completed",
                _encryptionService.GetCurrentKeyId(),
                _encryptionService.Encrypt(request.Description),
                _encryptionService.Encrypt(request.Metadata),
                request.SourceType,
                request.SourceCategory,
                // Pass timestamp for backdating support - Year/Month will be derived from this
                allowBackdate ? DateTimeOffset.FromUnixTimeMilliseconds(request.Timestamp).UtcDateTime : null);

            await _walletRepository.AddTransactionAsync(transaction, cancellationToken);
            await _walletRepository.SaveChangesAsync(cancellationToken);
            await _walletRepository.CommitTransactionAsync(cancellationToken);

            await PublishAsync(new WalletTransactionCreated(
                transaction.Id.ToString(),
                request.UserId,
                request.Amount,
                request.OperationId,
                request.Source,
                nowMs), WalletEventTopics.TransactionCreated, transaction.Id.ToString(), cancellationToken);

            await PublishAsync(new WalletBalanceChanged(
                request.UserId,
                balance,
                newBalance,
                request.Amount,
                request.Description), WalletEventTopics.BalanceChanged, request.UserId, cancellationToken);

            if (request.Source.Equals("promo", StringComparison.OrdinalIgnoreCase))
            {
                var promoId = "unknown";
                try
                {
                    using var json = JsonDocument.Parse(request.Metadata);
                    if (json.RootElement.TryGetProperty("promo_id", out var promoProp))
                    {
                        promoId = promoProp.GetString() ?? "unknown";
                    }
                }
                catch
                {
                    // best-effort metadata parsing
                }

                await PublishAsync(new PromoAwarded(
                    request.UserId,
                    request.Amount,
                    promoId,
                    request.OperationId), WalletEventTopics.PromoAwarded, request.UserId, cancellationToken);
            }

            return new AccruePointsResult(true, "Completed", newBalance);
        }
        catch (RPlus.Wallet.Domain.Exceptions.ConcurrencyException)
        {
            await _walletRepository.RollbackTransactionAsync(cancellationToken);
            return new AccruePointsResult(false, "Failed", 0, "CONCURRENCY_ERROR");
        }
        catch (Exception ex)
        {
            await _walletRepository.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Unhandled error processing accrual");
            return new AccruePointsResult(false, "Failed", 0, "INTERNAL_ERROR");
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
