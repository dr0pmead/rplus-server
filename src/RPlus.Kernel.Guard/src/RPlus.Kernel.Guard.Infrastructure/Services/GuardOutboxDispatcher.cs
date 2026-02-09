using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Infrastructure.Outbox;
using RPlus.Kernel.Guard.Infrastructure.Persistence;
using System;

namespace RPlus.Kernel.Guard.Infrastructure.Services;

public class GuardOutboxDispatcher : OutboxDispatcher<GuardDbContext>
{
    public GuardOutboxDispatcher(IServiceProvider serviceProvider, ILogger<GuardOutboxDispatcher> logger) 
        : base(serviceProvider, logger)
    {
    }

    protected override DbSet<OutboxMessage> GetOutbox(GuardDbContext dbContext)
    {
        return dbContext.OutboxMessages;
    }
}
