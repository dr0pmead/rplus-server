using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Wallet.Commands;
using RPlus.SDK.Wallet.Events;
using RPlus.SDK.Wallet.Results;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;
using RPlus.SDK.Eventing.Abstractions;

#nullable enable
namespace RPlus.Wallet.Application.Commands.CommitReserve;

public class CommitReserveHandler : IRequestHandler<CommitReserveCommand, CommitReserveResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<CommitReserveHandler> _logger;

    public CommitReserveHandler(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        IEventPublisher eventPublisher,
        ILogger<CommitReserveHandler> logger)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CommitReserveResult> Handle(CommitReserveCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _walletRepository.GetTransactionByOperationIdAsync(request.OperationId, cancellationToken);
        if (transaction == null || transaction.Status != "Pending")
        {
            return new CommitReserveResult(false, 0, "PENDING_TRANSACTION_NOT_FOUND");
        }

        await _walletRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            var wallet = await _walletRepository.GetByUserIdForUpdateAsync(Guid.Parse(request.UserId), cancellationToken);
            if (wallet == null)
            {
                return new CommitReserveResult(false, 0, "WALLET_NOT_FOUND");
            }

            var amount = _encryptionService.DecryptLong(transaction.AmountEncrypted, transaction.KeyId);
            var actualBalance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
            var reservedBalance = _encryptionService.DecryptLong(wallet.ReservedBalanceEncrypted, wallet.BalanceKeyId);
            var newBalance = actualBalance - amount;
            var newReserved = reservedBalance - amount;

            wallet.UpdateBalance(_encryptionService.Encrypt(newBalance), _encryptionService.GetCurrentKeyId());
            wallet.UpdateReserved(_encryptionService.Encrypt(newReserved), _encryptionService.GetCurrentKeyId());
            wallet.Version += 1;
            transaction.Status = "Completed";

            await _walletRepository.UpdateAsync(wallet, cancellationToken);
            await _walletRepository.SaveChangesAsync(cancellationToken);
            await _walletRepository.CommitTransactionAsync(cancellationToken);

            await PublishAsync(new WalletBalanceChanged(
                request.UserId,
                actualBalance,
                newBalance,
                -amount,
                "Commit Reserve"), WalletEventTopics.BalanceChanged, request.UserId, cancellationToken);

            await PublishAsync(new WalletTransactionCreated(
                transaction.Id.ToString(),
                request.UserId,
                amount,
                request.OperationId,
                transaction.Source,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), WalletEventTopics.TransactionCreated, transaction.Id.ToString(), cancellationToken);

            await PublishAsync(new WalletTransactionCommitted(
                transaction.Id.ToString(),
                request.UserId,
                request.OperationId), WalletEventTopics.TransactionCommitted, transaction.Id.ToString(), cancellationToken);

            return new CommitReserveResult(true, newBalance);
        }
        catch (Exception ex)
        {
            await _walletRepository.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Commit reserve error");
            return new CommitReserveResult(false, 0, "INTERNAL_ERROR");
        }
    }

    private Task PublishAsync<T>(T payload, string topic, string aggregateId, CancellationToken ct) where T : class =>
        _eventPublisher.PublishAsync(payload, topic, aggregateId, ct);
}
