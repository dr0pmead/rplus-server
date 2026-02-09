using RPlus.SDK.Users.Enums;

namespace RPlus.Users.Application.Interfaces.Messaging;

public interface IUserEventPublisher
{
    // FIO fields removed - now managed by HR module
    Task PublishUserCreatedAsync(
        Guid userId,
        string status,
        DateTime createdAt,
        CancellationToken ct = default);

    Task PublishUserStatusChangedAsync(
        Guid userId,
        UserStatus newStatus,
        DateTime changedAt,
        CancellationToken ct = default);

    Task PublishUserProfileUpdatedAsync(
        Guid userId,
        Guid? tenantId,
        Guid? nodeId,
        List<Guid> roleIds,
        DateTime updatedAt,
        CancellationToken ct = default);
}
