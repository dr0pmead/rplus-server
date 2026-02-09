using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Wallet.Queries;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;

#nullable enable
namespace RPlus.Wallet.Application.Queries.CheckStatus;

public class CheckStatusHandler : IRequestHandler<CheckStatusQuery, CheckStatusResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<CheckStatusHandler> _logger;

    public CheckStatusHandler(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        ILogger<CheckStatusHandler> logger)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<CheckStatusResult> Handle(CheckStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                return new CheckStatusResult("INVALID_USER_ID");
            }

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken);
            if (wallet == null)
            {
                return new CheckStatusResult("WalletNotFound");
            }

            var balance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
            var reserved = _encryptionService.DecryptLong(wallet.ReservedBalanceEncrypted, wallet.BalanceKeyId);

            if (string.IsNullOrEmpty(request.OperationId))
            {
                return new CheckStatusResult("SERVING", balance, reserved);
            }

            var transaction = await _walletRepository.GetTransactionByOperationIdAsync(request.OperationId, cancellationToken);
            return transaction == null
                ? new CheckStatusResult("TransactionNotFound", balance, reserved)
                : new CheckStatusResult(transaction.Status, balance, reserved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate wallet status");
            return new CheckStatusResult("DEGRADED", LastError: ex.Message);
        }
    }
}
