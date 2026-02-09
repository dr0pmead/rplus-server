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
namespace RPlus.Wallet.Application.Commands.CancelReserve;

public class CancelReserveHandler : IRequestHandler<CancelReserveCommand, CancelReserveResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<CancelReserveHandler> _logger;

    public CancelReserveHandler(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        IEventPublisher eventPublisher,
        ILogger<CancelReserveHandler> logger)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CancelReserveResult> Handle(CancelReserveCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _walletRepository.GetTransactionByOperationIdAsync(request.OperationId, cancellationToken);
        if (transaction == null || transaction.Status != "Pending")
        {
            return new CancelReserveResult(false, 0, "PENDING_TRANSACTION_NOT_FOUND");
        }

        await _walletRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            var wallet = await _walletRepository.GetByUserIdForUpdateAsync(Guid.Parse(request.UserId), cancellationToken);
            if (wallet == null)
            {
                return new CancelReserveResult(false, 0, "WALLET_NOT_FOUND");
            }

            var amount = _encryptionService.DecryptLong(transaction.AmountEncrypted, transaction.KeyId);
            var actualBalance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
            var reservedBalance = _encryptionService.DecryptLong(wallet.ReservedBalanceEncrypted, wallet.BalanceKeyId) - amount;

            wallet.UpdateReserved(_encryptionService.Encrypt(reservedBalance), _encryptionService.GetCurrentKeyId());
            wallet.Version += 1;
            transaction.Status = "Cancelled";

            await _walletRepository.UpdateAsync(wallet, cancellationToken);
            await _walletRepository.SaveChangesAsync(cancellationToken);
            await _walletRepository.CommitTransactionAsync(cancellationToken);

            await PublishAsync(new WalletTransactionCancelled(
                transaction.Id.ToString(),
                request.UserId,
                request.OperationId), WalletEventTopics.TransactionCancelled, transaction.Id.ToString(), cancellationToken);

            return new CancelReserveResult(true, actualBalance);
        }
        catch (Exception ex)
        {
            await _walletRepository.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Cancel reserve error");
            return new CancelReserveResult(false, 0, "INTERNAL_ERROR");
        }
    }

    private Task PublishAsync<T>(T payload, string topic, string aggregateId, CancellationToken ct) where T : class =>
        _eventPublisher.PublishAsync(payload, topic, aggregateId, ct);
}
