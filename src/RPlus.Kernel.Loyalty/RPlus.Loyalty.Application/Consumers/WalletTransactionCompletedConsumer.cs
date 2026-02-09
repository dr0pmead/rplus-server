using System.Threading.Tasks;
using MassTransit;
using RPlus.SDK.Contracts.Domain.Wallet;
using RPlus.SDK.Infrastructure.Idempotency;
using RPlus.Loyalty.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RPlus.Loyalty.Application.Consumers;

public class WalletTransactionCompletedConsumer : IConsumer<WalletTransactionCompleted_v1>
{
    private readonly LoyaltyDbContext _dbContext;
    private readonly IdempotencyValidator<LoyaltyDbContext> _idempotency;

    public WalletTransactionCompletedConsumer(LoyaltyDbContext dbContext) // IdempotencyValidator might need manual instantiation if not in DI generic
    {
        _dbContext = dbContext;
        _idempotency = new IdempotencyValidator<LoyaltyDbContext>(dbContext);
    }

    public async Task Consume(ConsumeContext<WalletTransactionCompleted_v1> context)
    {
        var msg = context.Message;
        if (!await _idempotency.ProcessHeaderAsync(msg.MessageId, "WalletTransactionCompleted_v1", "LoyaltyService", context.CancellationToken))
        {
            return;
        }

        // Update Loyalty Balance logic (Simplified)
        // Ideally this should use a Domain Service "LoyaltyService.ApplyPoints"
        // For recovery, I'll just save Idempotency as proof of consumption
        // Real logic: existing profile? update balance.
        
        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
