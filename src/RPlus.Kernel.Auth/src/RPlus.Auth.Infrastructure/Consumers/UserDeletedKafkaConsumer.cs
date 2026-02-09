// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Consumers.UserDeletedKafkaConsumer
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

public sealed class UserDeletedKafkaConsumer : 
  KafkaConsumerBackgroundService<string, UserDeletedEvent>
{
  private readonly IServiceProvider _serviceProvider;

  public UserDeletedKafkaConsumer(
    IOptions<KafkaOptions> options,
    IServiceProvider serviceProvider,
    ILogger<UserDeletedKafkaConsumer> logger)
    : base(options, (ILogger) logger, "users.deleted")
  {
    this._serviceProvider = serviceProvider;
  }

  protected override async Task HandleMessageAsync(
    string key,
    UserDeletedEvent message,
    CancellationToken cancellationToken)
  {
    UserDeletedKafkaConsumer deletedKafkaConsumer = this;
    IServiceScope scope = deletedKafkaConsumer._serviceProvider.CreateScope();
    try
    {
      AuthDbContext db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
      AuthKnownUserEntity authKnownUserEntity = await db.AuthKnownUsers.FirstOrDefaultAsync<AuthKnownUserEntity>((Expression<Func<AuthKnownUserEntity, bool>>) (u => u.UserId == message.UserId), cancellationToken);
      List<AuthSessionEntity> sessions;
      List<RefreshTokenEntity> refreshTokens;
      if (authKnownUserEntity == null)
      {
        deletedKafkaConsumer._logger.LogWarning("User {UserId} not found in AuthKnownUsers, skipping deletion", (object) message.UserId);
        scope = (IServiceScope) null;
        db = (AuthDbContext) null;
        sessions = (List<AuthSessionEntity>) null;
        refreshTokens = (List<RefreshTokenEntity>) null;
      }
      else
      {
        authKnownUserEntity.IsActive = false;
        authKnownUserEntity.UpdatedAt = DateTime.UtcNow;
        int num1 = await db.SaveChangesAsync(cancellationToken);
        sessions = await db.AuthSessions.Where<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (s => s.UserId == message.UserId && s.RevokedAt == new DateTime?())).ToListAsync<AuthSessionEntity>(cancellationToken);
        foreach (AuthSessionEntity authSessionEntity in sessions)
        {
          authSessionEntity.RevokedAt = new DateTime?(DateTime.UtcNow);
          authSessionEntity.RevokeReason = "user_deleted: " + message.Reason;
        }
        refreshTokens = await db.RefreshTokens.Where<RefreshTokenEntity>((Expression<Func<RefreshTokenEntity, bool>>) (rt => rt.UserId == message.UserId && rt.RevokedAt == new DateTime?())).ToListAsync<RefreshTokenEntity>(cancellationToken);
        foreach (RefreshTokenEntity refreshTokenEntity in refreshTokens)
          refreshTokenEntity.RevokedAt = new DateTime?(DateTime.UtcNow);
        int num2 = await db.SaveChangesAsync(cancellationToken);
        deletedKafkaConsumer._logger.LogWarning("User {UserId} deleted: marked as inactive, {SessionCount} sessions and {TokenCount} tokens revoked (Reason: {Reason})", (object) message.UserId, (object) sessions.Count, (object) refreshTokens.Count, (object) message.Reason);
        scope = (IServiceScope) null;
        db = (AuthDbContext) null;
        sessions = (List<AuthSessionEntity>) null;
        refreshTokens = (List<RefreshTokenEntity>) null;
      }
    }
    finally
    {
      scope?.Dispose();
    }
  }
}
