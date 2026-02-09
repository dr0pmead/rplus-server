using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Wallet.Persistence;
using RPlus.SDK.Infrastructure.Outbox;

namespace RPlus.Wallet.Infrastructure.Services;

public class WalletOutboxDispatcher : OutboxDispatcher<WalletDbContext>
{
    public WalletOutboxDispatcher(IServiceProvider serviceProvider, ILogger<WalletOutboxDispatcher> logger) 
        : base(serviceProvider, logger)
    {
    }

    protected override DbSet<OutboxMessage> GetOutbox(WalletDbContext dbContext)
    {
        return dbContext.OutboxMessages;
    }
}
