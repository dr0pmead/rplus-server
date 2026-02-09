using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Eventing.Abstractions;

namespace RPlus.SDK.Infrastructure.Outbox;

public abstract class OutboxDispatcher<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    protected OutboxDispatcher(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected abstract DbSet<OutboxMessage> GetOutbox(TDbContext dbContext);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var messages = await GetOutbox(dbContext)
                    .Where(m => m.PublishedAt == null)
                    .OrderBy(m => m.CreatedAt)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var msg in messages)
                {
                    await publisher.PublishRawAsync(msg.EventName, msg.Payload, msg.AggregateId, stoppingToken);
                    msg.PublishedAt = DateTime.UtcNow;
                }

                if (messages.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                else
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
