using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Wallet.Commands;
using RPlus.SDK.Wallet.Events;
using RPlus.SDK.Wallet.Results;
using RPlus.Wallet.Domain.Entities;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;
using RPlus.SDK.Eventing.Abstractions;

#nullable enable
namespace RPlus.Wallet.Application.Commands.ReverseTransaction;

public class ReverseTransactionHandler : IRequestHandler<ReverseTransactionCommand, ReverseTransactionResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ReverseTransactionHandler> _logger;

    public ReverseTransactionHandler(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        IEventPublisher eventPublisher,
        ILogger<ReverseTransactionHandler> logger)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<ReverseTransactionResult> Handle(ReverseTransactionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing reversal for {OperationId}", request.OriginalOperationId);

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new ReverseTransactionResult(false, 0, "INVALID_USER_ID");
        }

        await _walletRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            var wallet = await _walletRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
            if (wallet == null)
            {
                await _walletRepository.RollbackTransactionAsync(cancellationToken);
                return new ReverseTransactionResult(false, 0, "WALLET_NOT_FOUND");
            }

            var originalTx = await _walletRepository.GetTransactionByOperationIdAsync(request.OriginalOperationId, cancellationToken);
            if (originalTx == null)
            {
                await _walletRepository.RollbackTransactionAsync(cancellationToken);
                return new ReverseTransactionResult(false, 0, "TRANSACTION_NOT_FOUND");
            }

            var reversalOperationId = $"rev-{request.OriginalOperationId}";
            if (await _walletRepository.GetTransactionByOperationIdAsync(reversalOperationId, cancellationToken) != null)
            {
                await _walletRepository.CommitTransactionAsync(cancellationToken);
                var currentBalance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
                return new ReverseTransactionResult(true, currentBalance);
            }

            var originalAmount = _encryptionService.DecryptLong(originalTx.AmountEncrypted, originalTx.KeyId);
            var reversalAmount = -originalAmount;
            var current = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
            var newBalance = current + reversalAmount;
            if (newBalance < 0)
            {
                await _walletRepository.RollbackTransactionAsync(cancellationToken);
                return new ReverseTransactionResult(false, 0, "INSUFFICIENT_FUNDS_FOR_REVERSAL");
            }

            var metadata = JsonSerializer.Serialize(new
            {
                original_operation_id = request.OriginalOperationId,
                reason = request.Reason
            });

            var reversalTransaction = WalletTransaction.Create(
                userId,
                reversalOperationId,
                Guid.NewGuid().ToString(),
                _encryptionService.Encrypt(reversalAmount),
                _encryptionService.Encrypt(current),
                _encryptionService.Encrypt(newBalance),
                "reversal",
                "Completed",
                _encryptionService.GetCurrentKeyId(),
                _encryptionService.Encrypt($"Reversal of {request.OriginalOperationId}"),
                _encryptionService.Encrypt(metadata));

            wallet.UpdateBalance(_encryptionService.Encrypt(newBalance), _encryptionService.GetCurrentKeyId());
            wallet.Version += 1;

            await _walletRepository.AddTransactionAsync(reversalTransaction, cancellationToken);
            await _walletRepository.UpdateAsync(wallet, cancellationToken);
            await _walletRepository.SaveChangesAsync(cancellationToken);
            await _walletRepository.CommitTransactionAsync(cancellationToken);

            await PublishAsync(new WalletTransactionCreated(
                reversalTransaction.Id.ToString(),
                request.UserId,
                reversalAmount,
                reversalOperationId,
                "reversal",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), WalletEventTopics.TransactionCreated, reversalTransaction.Id.ToString(), cancellationToken);

            await PublishAsync(new WalletTransactionReversed(
                request.OriginalOperationId,
                reversalOperationId,
                request.UserId,
                originalAmount,
                request.Reason), WalletEventTopics.TransactionReversed, request.OriginalOperationId, cancellationToken);

            await PublishAsync(new WalletBalanceChanged(
                request.UserId,
                current,
                newBalance,
                reversalAmount,
                "Reversal"), WalletEventTopics.BalanceChanged, request.UserId, cancellationToken);

            return new ReverseTransactionResult(true, newBalance);
        }
        catch (Exception ex)
        {
            await _walletRepository.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Failed to reverse transaction {OperationId}", request.OriginalOperationId);
            return new ReverseTransactionResult(false, 0, "INTERNAL_ERROR");
        }
    }

    private Task PublishAsync<T>(T payload, string topic, string aggregateId, CancellationToken ct) where T : class =>
        _eventPublisher.PublishAsync(payload, topic, aggregateId, ct);
}
