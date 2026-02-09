using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.HR.Api.Services;
using RPlus.HR.Domain.Entities;
using RPlus.HR.Infrastructure.Persistence;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Eventing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.HR.Api.Consumers;

/// <summary>
/// Ensures HR profile exists for staff users.
/// Staff provisioning events are published by Users service on successful staff creation.
/// </summary>
public sealed class HrStaffUserProvisioningConsumer : KafkaConsumer<EventEnvelope<UserCreated>>
{
    private readonly IServiceProvider _serviceProvider;

    public HrStaffUserProvisioningConsumer(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> options,
        ILogger<HrStaffUserProvisioningConsumer> logger)
        : base(options, topic: "users.user.created.v1", groupId: "rplus-hr-users-created-group", logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleAsync(EventEnvelope<UserCreated> message, CancellationToken ct)
    {
        var payload = message.Payload;
        if (payload is null)
            return;

        if (!Guid.TryParse(payload.UserId, out var userId) || userId == Guid.Empty)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var documents = scope.ServiceProvider.GetRequiredService<DocumentsGateway>();

        var existing = await db.EmployeeProfiles.FindAsync(new object[] { userId }, ct);
        var now = DateTime.UtcNow;
        var createdAt = payload.CreatedAt == default ? now : payload.CreatedAt;

        if (existing != null)
        {
            if (existing.DocumentsFolderId == null)
            {
                var folderId = await documents.EnsureUserFolderAsync(userId, ct);
                if (folderId.HasValue)
                {
                    existing.DocumentsFolderId = folderId.Value;
                    existing.UpdatedAt = now;
                }
            }

            if (db.ChangeTracker.HasChanges())
            {
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update HR profile for user {UserId}", userId);
                }
            }

            return;
        }

        var createdFolder = await documents.EnsureUserFolderAsync(userId, ct);
        db.EmployeeProfiles.Add(new EmployeeProfile
        {
            UserId = userId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Status = EmployeeStatus.Active,
            Citizenship = "KZ",
            DocumentsFolderId = createdFolder
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Fail-open: provisioning must not break HR service.
            // If a concurrent creator already inserted, EF will throw; safe to ignore.
            _logger.LogWarning(ex, "Failed to auto-provision HR profile for user {UserId}", userId);
        }
    }
}
