using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Infrastructure.Outbox;

namespace RPlus.Loyalty.Infrastructure.Services;

public class LoyaltyOutboxDispatcher : OutboxDispatcher<LoyaltyDbContext>
{
    public LoyaltyOutboxDispatcher(IServiceProvider serviceProvider, ILogger<LoyaltyOutboxDispatcher> logger) 
        : base(serviceProvider, logger)
    {
    }

    protected override DbSet<OutboxMessage> GetOutbox(LoyaltyDbContext dbContext)
    {
        return dbContext.OutboxMessages;
    }
}
