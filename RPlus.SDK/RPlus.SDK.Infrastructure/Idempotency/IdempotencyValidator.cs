using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace RPlus.SDK.Infrastructure.Idempotency;

public class IdempotencyValidator<TDbContext> where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    public IdempotencyValidator(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ProcessHeaderAsync(Guid messageId, string eventName, string consumerName, CancellationToken cancellationToken)
    {
        // Require ProcessedMessages DbSet? Or use raw SQL?
        // Ideally generic TDbContext expects a DbSet<ProcessedMessage> property OR we use Set<ProcessedMessage>()
        // Assuming TDbContext has the entity registered.
        var set = _dbContext.Set<ProcessedMessage>();
        
        var exists = await set.AnyAsync(x => x.MessageId == messageId, cancellationToken);
        if (exists) return false;

        set.Add(new ProcessedMessage
        {
            MessageId = messageId,
            Consumer = consumerName,
            EventName = eventName,
            ProcessedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
