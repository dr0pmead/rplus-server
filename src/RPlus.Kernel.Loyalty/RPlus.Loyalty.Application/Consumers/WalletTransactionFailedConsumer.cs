using System.Threading.Tasks;
using MassTransit;
using RPlus.SDK.Contracts.Domain.Wallet;
using RPlus.SDK.Infrastructure.Idempotency;
using RPlus.Loyalty.Persistence;

namespace RPlus.Loyalty.Application.Consumers;

public class WalletTransactionFailedConsumer : IConsumer<WalletTransactionFailed_v1>
{
    private readonly LoyaltyDbContext _dbContext;
    private readonly IdempotencyValidator<LoyaltyDbContext> _idempotency;

    public WalletTransactionFailedConsumer(LoyaltyDbContext dbContext)
    {
        _dbContext = dbContext;
        _idempotency = new IdempotencyValidator<LoyaltyDbContext>(dbContext);
    }

    public async Task Consume(ConsumeContext<WalletTransactionFailed_v1> context)
    {
        if (!await _idempotency.ProcessHeaderAsync(context.Message.MessageId, "WalletTransactionFailed_v1", "LoyaltyService", context.CancellationToken))
             return;
        
        // Handle failure (Compensating action)
    }
}
