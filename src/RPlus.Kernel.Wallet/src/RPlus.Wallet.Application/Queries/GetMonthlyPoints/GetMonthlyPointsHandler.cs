using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RPlus.SDK.Wallet.Queries;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;

#nullable enable
namespace RPlus.Wallet.Application.Queries.GetMonthlyPoints;

public class GetMonthlyPointsHandler : IRequestHandler<GetMonthlyPointsQuery, GetMonthlyPointsResult>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;

    public GetMonthlyPointsHandler(IWalletRepository walletRepository, IEncryptionService encryptionService)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
    }

    public async Task<GetMonthlyPointsResult> Handle(GetMonthlyPointsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                return new GetMonthlyPointsResult(0, 0, false, "Invalid UserId");
            }

            var transactions = await _walletRepository.GetMonthlyTransactionsAsync(
                userId,
                request.Year,
                request.Month,
                request.SourceTypes,
                cancellationToken);

            // Decrypt amounts and aggregate
            long totalPoints = 0;
            foreach (var tx in transactions)
            {
                try
                {
                    var amount = _encryptionService.DecryptLong(tx.AmountEncrypted, tx.KeyId);
                    totalPoints += amount;
                }
                catch
                {
                    // Skip transactions that fail to decrypt
                }
            }

            return new GetMonthlyPointsResult(totalPoints, transactions.Count, true);
        }
        catch (Exception ex)
        {
            return new GetMonthlyPointsResult(0, 0, false, ex.Message);
        }
    }
}
