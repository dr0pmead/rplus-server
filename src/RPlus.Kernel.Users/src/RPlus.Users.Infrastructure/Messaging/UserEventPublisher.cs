using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Eventing;
using RPlus.SDK.Users.Enums;
using RPlus.Users.Application.Interfaces.Messaging;
using RPlus.Users.Domain.Entities;
using RPlus.Users.Infrastructure.Persistence;
using System.Text.Json;

namespace RPlus.Users.Infrastructure.Messaging;

public sealed class UserEventPublisher : IUserEventPublisher
{
    private readonly UsersDbContext _db;

    public UserEventPublisher(UsersDbContext db) => _db = db;

    // FIO fields removed - now managed by HR module
    public async Task PublishUserCreatedAsync(
        Guid userId,
        string status,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        var userCreated = new UserCreated(
            userId.ToString(),
            string.Empty,
            null,
            Array.Empty<string>(),
            createdAt);

        var eventEnvelope = new EventEnvelope<UserCreated>(
            userCreated,
            "rplus.users",
            "users.user.created.v1",
            userId.ToString(),
            Guid.NewGuid());

        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = eventEnvelope.EventId,
            Topic = "users.user.created.v1",
            EventType = eventEnvelope.EventType,
            AggregateId = eventEnvelope.AggregateId,
            Payload = JsonSerializer.Serialize(eventEnvelope),
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        });

        await Task.CompletedTask;
    }

    public async Task PublishUserStatusChangedAsync(
        Guid userId,
        UserStatus newStatus,
        DateTime changedAt,
        CancellationToken ct = default)
    {
        var changedFields = new Dictionary<string, string>
        {
            { "Status", newStatus.ToString() }
        };

        var eventEnvelope = new EventEnvelope<UserUpdated>(
            new UserUpdated(userId.ToString(), changedFields, changedAt),
            "rplus.users",
            "users.user.updated.v1",
            userId.ToString(),
            Guid.NewGuid());

        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = eventEnvelope.EventId,
            Topic = "users.user.updated.v1",
            EventType = eventEnvelope.EventType,
            AggregateId = eventEnvelope.AggregateId,
            Payload = JsonSerializer.Serialize(eventEnvelope),
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task PublishUserProfileUpdatedAsync(
        Guid userId,
        Guid? tenantId,
        Guid? nodeId,
        List<Guid> roleIds,
        DateTime updatedAt,
        CancellationToken ct = default)
    {
        var changedFields = new Dictionary<string, string>();

        if (tenantId.HasValue)
            changedFields.Add("TenantId", tenantId.Value.ToString());

        if (nodeId.HasValue)
            changedFields.Add("NodeId", nodeId.Value.ToString());

        var eventEnvelope = new EventEnvelope<UserUpdated>(
            new UserUpdated(userId.ToString(), changedFields, updatedAt),
            "rplus.users",
            "users.user.updated.v1",
            userId.ToString(),
            Guid.NewGuid());

        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = eventEnvelope.EventId,
            Topic = "users.user.updated.v1",
            EventType = eventEnvelope.EventType,
            AggregateId = eventEnvelope.AggregateId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(eventEnvelope)
        });

        await _db.SaveChangesAsync(ct);
    }
}
