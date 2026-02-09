using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Core.Messaging.Events;

namespace RPlus.Loyalty.Infrastructure.Consumers;

public sealed class UserCreatedConsumer : KafkaConsumerBackgroundService<string, UserCreated>
{
    private readonly IDbContextFactory<LoyaltyDbContext> _dbFactory;

    public UserCreatedConsumer(
        IOptions<KafkaOptions> options,
        IDbContextFactory<LoyaltyDbContext> dbFactory,
        ILogger<UserCreatedConsumer> logger)
        : base(options, logger, "user.identity.v1")
    {
        _dbFactory = dbFactory;
    }

    protected override async Task HandleMessageAsync(string key, UserCreated message, CancellationToken cancellationToken)
    {
        // Skip system/root users from loyalty provisioning.
        if (string.Equals(message.UserType, "System", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var changed = false;
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == message.UserId, cancellationToken);
        if (profile is null)
        {
            db.Profiles.Add(LoyaltyProfile.Create(message.UserId));
            changed = true;
        }

        var program = await db.ProgramProfiles.FirstOrDefaultAsync(p => p.UserId == message.UserId, cancellationToken);
        if (program is null)
        {
            db.ProgramProfiles.Add(new LoyaltyProgramProfile
            {
                UserId = message.UserId,
                Level = "base",
                TagsJson = "[]",
                PointsBalance = 0,
                Discount = 0,
                MotivationDiscount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            changed = true;
        }

        if (!changed)
            return;

        await db.SaveChangesAsync(cancellationToken);
    }
}
