using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.SDK.Core.Messaging.Events;
using RPlus.Users.Application.Interfaces.Messaging;
using RPlus.Users.Domain.Entities;
using RPlus.Users.Infrastructure.Persistence;

namespace RPlus.Users.Infrastructure.Consumers;

public class UserCreatedConsumer : KafkaConsumer<UserCreated>
{
    private readonly IServiceProvider _serviceProvider;

    public UserCreatedConsumer(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> options,
        ILogger<UserCreatedConsumer> logger)
        : base(options, "user.identity.v1", "rplus-users-service-group", logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleAsync(UserCreated message, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        // Exclude root/system users from provisioning across bounded contexts.
        if (string.Equals(message.UserType, "System", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Skipping Users profile creation for system user {UserId}. UserType={UserType}",
                message.UserId,
                message.UserType);
            return;
        }

        var existing = await db.Users.FindAsync(new object[] { message.UserId }, ct);
        if (existing != null)
        {
            _logger.LogInformation("User {UserId} already exists in Users service. Skipping creation.", message.UserId);
            return;
        }

        var preferredName = string.IsNullOrEmpty(message.Login)
            ? (message.FirstName ?? "New User")
            : message.Login;

        // FIO fields removed - now managed by HR module
        await db.Users.AddAsync(
            UserEntity.Create(
                message.UserId,
                preferredName,
                "ru-RU",
                "UTC",
                message.OccurredAt),
            ct);

        // Publish public "users.user.created.v1" for staff provisioning (wallet, loyalty, etc) via Users outbox.
        // (Fail-open: if publisher is unavailable, user profile creation still succeeds.)
        try
        {
            var publisher = scope.ServiceProvider.GetService<IUserEventPublisher>();
            if (publisher != null)
            {
                // FIO removed from event
                await publisher.PublishUserCreatedAsync(
                    message.UserId,
                    status: "Active",
                    createdAt: message.OccurredAt,
                    ct: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue users.user.created.v1 outbox message for {UserId}", message.UserId);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Created User profile for {UserId} (Source: {Source})", message.UserId, message.Source);
    }
}
