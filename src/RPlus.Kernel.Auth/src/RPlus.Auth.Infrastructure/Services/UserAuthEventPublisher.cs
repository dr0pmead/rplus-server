// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.UserAuthEventPublisher
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.Core.Contracts.Events;
using RPlus.SDK.Core.Messaging.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public class UserAuthEventPublisher : IUserAuthEventPublisher
{
  private readonly AuthDbContext _db;
  private readonly IOutboxRepository _outboxRepository;
  private readonly ILogger<UserAuthEventPublisher> _logger;

  public UserAuthEventPublisher(
    AuthDbContext db,
    IOutboxRepository outboxRepository,
    ILogger<UserAuthEventPublisher> logger)
  {
    this._db = db;
    this._outboxRepository = outboxRepository;
    this._logger = logger;
  }

  public async Task PublishUserCreatedAsync(
    AuthUserEntity user,
    string? firstName,
    string? lastName,
    string? middleName,
    System.Collections.Generic.Dictionary<string, string>? properties,
    CancellationToken ct)
  {
    try
    {
      UserCreated userCreated = new UserCreated(
          Guid.NewGuid(), 
          DateTime.UtcNow, 
          user.Id, 
          user.TenantId, 
          user.Login ?? string.Empty, 
          user.Email ?? string.Empty, 
          user.PhoneHash ?? string.Empty, 
          "RPlus.Auth", 
          user.UserType.ToString(), 
          firstName, 
          lastName, 
          middleName,
          properties
      );
      await this._outboxRepository.AddAsync(new OutboxMessageEntity()
      {
        Id = Guid.NewGuid(),
        Topic = "user.identity.v1",
        EventType = "UserCreated",
        AggregateId = user.Id.ToString(),
        Payload = JsonSerializer.Serialize<UserCreated>(userCreated, new JsonSerializerOptions()
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }),
        CreatedAt = DateTime.UtcNow,
        Status = "Pending"
      }, ct);
      this._logger.LogInformation("Queued SDK UserCreated event for user {UserId} (Tenant: {TenantId})", (object) user.Id, (object) user.TenantId);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish UserCreatedEvent for user {UserId}", (object) user.Id);
    }
  }

  public async Task PublishUserAuthUpdatedAsync(
    Guid userId,
    string? ipAddress,
    CancellationToken ct)
  {
    try
    {
      AuthUserEntity user = await this._db.AuthUsers.FindAsync(new object[1]
      {
        (object) userId
      }, ct);
      if (user == null)
      {
        this._logger.LogWarning("Cannot publish UserAuthEvent: User {UserId} not found", (object) userId);
      }
      else
      {
        bool hasPasskeys = await this._db.PasskeyCredentials.AnyAsync<PasskeyCredentialEntity>((Expression<Func<PasskeyCredentialEntity, bool>>) (x => x.UserId == userId), ct);
        AuthSessionEntity authSessionEntity = await this._db.AuthSessions.Where<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (x => x.UserId == userId)).OrderByDescending<AuthSessionEntity, DateTime>((Expression<Func<AuthSessionEntity, DateTime>>) (x => x.IssuedAt)).FirstOrDefaultAsync<AuthSessionEntity>(ct);
        List<string> stringList = new List<string>();
        if (!string.IsNullOrEmpty(user.PhoneHash))
          stringList.Add("sms");
        if (hasPasskeys)
          stringList.Add("passkey");
        UserAuthEvent userAuthEvent = new UserAuthEvent(userId, user.IsBlocked, authSessionEntity?.IssuedAt, ipAddress ?? authSessionEntity?.IssuerIp, stringList.ToArray(), DateTime.UtcNow);
        await this._outboxRepository.AddAsync(new OutboxMessageEntity()
        {
          Id = Guid.NewGuid(),
          Topic = "user.auth.v1",
          EventType = "UserAuthUpdated",
          AggregateId = userId.ToString(),
          Payload = JsonSerializer.Serialize<UserAuthEvent>(userAuthEvent),
          CreatedAt = DateTime.UtcNow,
          Status = "Pending",
          RetryCount = 0,
          MaxRetries = 3
        }, ct);
        this._logger.LogInformation("Queued UserAuthEvent for user {UserId}", (object) userId);
        user = (AuthUserEntity) null;
      }
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish UserAuthEvent for user {UserId}", (object) userId);
    }
  }

  public async Task PublishUserTerminatedAsync(
    Guid userId,
    string reason,
    CancellationToken ct)
  {
    try
    {
      var user = await this._db.AuthUsers.FindAsync(new object[] { userId }, ct);
      if (user == null)
      {
        this._logger.LogWarning("Cannot publish UserTerminatedEvent: User {UserId} not found", userId);
        return;
      }

      var userTerminated = new UserTerminated(
          Guid.NewGuid(),
          DateTime.UtcNow,
          user.Id,
          user.TenantId,
          reason,
          "RPlus.Auth"
      );

      await this._outboxRepository.AddAsync(new OutboxMessageEntity()
      {
        Id = Guid.NewGuid(),
        Topic = "user.identity.v1",
        EventType = "UserTerminated",
        AggregateId = user.Id.ToString(),
        Payload = JsonSerializer.Serialize(userTerminated, new JsonSerializerOptions()
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }),
        CreatedAt = DateTime.UtcNow,
        Status = "Pending"
      }, ct);

      this._logger.LogInformation("Queued UserTerminated event for user {UserId} (Tenant: {TenantId}). Reason: {Reason}",
        user.Id, user.TenantId, reason);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish UserTerminatedEvent for user {UserId}", userId);
    }
  }
}
