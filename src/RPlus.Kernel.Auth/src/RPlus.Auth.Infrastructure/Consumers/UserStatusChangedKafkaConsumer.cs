// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Consumers.UserStatusChangedKafkaConsumer
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Contracts.Events;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Consumers;

public sealed class UserStatusChangedKafkaConsumer : 
  KafkaConsumerBackgroundService<string, UserStatusChangedEvent>
{
  private readonly IServiceProvider _serviceProvider;

  public UserStatusChangedKafkaConsumer(
    IOptions<KafkaOptions> options,
    IServiceProvider serviceProvider,
    ILogger<UserStatusChangedKafkaConsumer> logger)
    : base(options, (ILogger) logger, "users.status_changed")
  {
    this._serviceProvider = serviceProvider;
  }

  protected override async Task HandleMessageAsync(
    string key,
    UserStatusChangedEvent message,
    CancellationToken cancellationToken)
  {
    UserStatusChangedKafkaConsumer changedKafkaConsumer = this;
    IServiceScope scope = changedKafkaConsumer._serviceProvider.CreateScope();
    try
    {
      AuthDbContext db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
      AuthKnownUserEntity authKnownUserEntity = await db.AuthKnownUsers.FirstOrDefaultAsync<AuthKnownUserEntity>((Expression<Func<AuthKnownUserEntity, bool>>) (u => u.UserId == message.UserId), cancellationToken);
      if (authKnownUserEntity == null)
      {
        changedKafkaConsumer._logger.LogWarning("User {UserId} not found in AuthKnownUsers, skipping status change", (object) message.UserId);
        scope = (IServiceScope) null;
        db = (AuthDbContext) null;
      }
      else
      {
        bool wasActive = authKnownUserEntity.IsActive;
        bool isActive = message.Status == "Active";
        if (wasActive == isActive)
        {
          changedKafkaConsumer._logger.LogDebug("User {UserId} status unchanged (IsActive={IsActive}), skipping", (object) message.UserId, (object) isActive);
          scope = (IServiceScope) null;
          db = (AuthDbContext) null;
        }
        else
        {
          authKnownUserEntity.IsActive = isActive;
          authKnownUserEntity.UpdatedAt = DateTime.UtcNow;
          int num1 = await db.SaveChangesAsync(cancellationToken);
          changedKafkaConsumer._logger.LogInformation("User {UserId} status changed: {OldStatus} → {NewStatus} (Reason: {Reason})", (object) message.UserId, wasActive ? (object) "Active" : (object) "Inactive", isActive ? (object) "Active" : (object) "Inactive", (object) message.Reason);
          if (isActive)
          {
            scope = (IServiceScope) null;
            db = (AuthDbContext) null;
          }
          else
          {
            List<AuthSessionEntity> sessions = await db.AuthSessions.Where<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (s => s.UserId == message.UserId && s.RevokedAt == new DateTime?())).ToListAsync<AuthSessionEntity>(cancellationToken);
            foreach (AuthSessionEntity authSessionEntity in sessions)
            {
              authSessionEntity.RevokedAt = new DateTime?(DateTime.UtcNow);
              authSessionEntity.RevokeReason = $"business_deactivation: {message.Status} - {message.Reason}";
            }
            List<RefreshTokenEntity> refreshTokens = await db.RefreshTokens.Where<RefreshTokenEntity>((Expression<Func<RefreshTokenEntity, bool>>) (rt => rt.UserId == message.UserId && rt.RevokedAt == new DateTime?())).ToListAsync<RefreshTokenEntity>(cancellationToken);
            foreach (RefreshTokenEntity refreshTokenEntity in refreshTokens)
              refreshTokenEntity.RevokedAt = new DateTime?(DateTime.UtcNow);
            int num2 = await db.SaveChangesAsync(cancellationToken);
            changedKafkaConsumer._logger.LogWarning("All sessions ({SessionCount}) and refresh tokens ({TokenCount}) revoked for user {UserId} due to business deactivation (Status: {Status})", (object) sessions.Count, (object) refreshTokens.Count, (object) message.UserId, (object) message.Status);
            sessions = (List<AuthSessionEntity>) null;
            refreshTokens = (List<RefreshTokenEntity>) null;
            scope = (IServiceScope) null;
            db = (AuthDbContext) null;
          }
        }
      }
    }
    finally
    {
      scope?.Dispose();
    }
  }
}
