using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RPlus.SDK.Wallet.Queries;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;

#nullable enable
namespace RPlus.Wallet.Application.Queries.GetHistory;

public class GetHistoryHandler : IRequestHandler<GetHistoryQuery, GetHistoryResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;

    public GetHistoryHandler(IWalletRepository walletRepository, IEncryptionService encryptionService)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
    }

    public async Task<GetHistoryResult> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _walletRepository.GetTransactionsAsync(
            Guid.Parse(request.UserId),
            request.Limit <= 0 ? 20 : request.Limit,
            request.Cursor,
            request.Source,
            cancellationToken);

        var items = new List<WalletTransactionDto>(transactions.Count);
        foreach (var transaction in transactions)
        {
            items.Add(new WalletTransactionDto
            {
                OperationId = transaction.OperationId,
                Amount = _encryptionService.DecryptLong(transaction.AmountEncrypted, transaction.KeyId),
                BalanceBefore = _encryptionService.DecryptLong(transaction.BalanceBeforeEncrypted, transaction.KeyId),
                BalanceAfter = _encryptionService.DecryptLong(transaction.BalanceAfterEncrypted, transaction.KeyId),
                Source = transaction.Source,
                Status = transaction.Status,
                CreatedAt = new DateTimeOffset(transaction.CreatedAt).ToUnixTimeMilliseconds(),
                ProcessedAt = transaction.ProcessedAt.HasValue
                    ? new DateTimeOffset(transaction.ProcessedAt.Value).ToUnixTimeMilliseconds()
                    : 0,
                Description = transaction.DescriptionEncrypted != null
                    ? _encryptionService.DecryptString(transaction.DescriptionEncrypted, transaction.KeyId)
                    : string.Empty,
                Metadata = transaction.MetadataEncrypted != null
                    ? _encryptionService.DecryptString(transaction.MetadataEncrypted, transaction.KeyId)
                    : string.Empty
            });
        }

        var nextCursor = transactions.Count == 0
            ? string.Empty
            : transactions.Last().CreatedAt.Ticks.ToString();

        return new GetHistoryResult(items, nextCursor);
    }
}
