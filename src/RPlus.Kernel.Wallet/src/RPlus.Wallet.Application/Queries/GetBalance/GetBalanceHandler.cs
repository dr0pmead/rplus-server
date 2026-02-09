using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RPlus.SDK.Wallet.Queries;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;

#nullable enable
namespace RPlus.Wallet.Application.Queries.GetBalance;

public class GetBalanceHandler : IRequestHandler<GetBalanceQuery, long>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;

    public GetBalanceHandler(IWalletRepository walletRepository, IEncryptionService encryptionService)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
    }

    public async Task<long> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(Guid.Parse(request.UserId), cancellationToken);
        return wallet == null
            ? 0
            : _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);
    }
}
