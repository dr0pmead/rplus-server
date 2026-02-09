// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Consumers.UserCreatedKafkaConsumer
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Contracts.Events;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Consumers;

public sealed class UserCreatedKafkaConsumer : 
  KafkaConsumerBackgroundService<string, UserCreatedEvent>
{
  private readonly IServiceProvider _serviceProvider;

  public UserCreatedKafkaConsumer(
    IOptions<KafkaOptions> options,
    IServiceProvider serviceProvider,
    ILogger<UserCreatedKafkaConsumer> logger)
    : base(options, (ILogger) logger, "users.created")
  {
    this._serviceProvider = serviceProvider;
  }

  protected override async Task HandleMessageAsync(
    string key,
    UserCreatedEvent message,
    CancellationToken cancellationToken)
  {
    UserCreatedKafkaConsumer createdKafkaConsumer = this;
    IServiceScope scope = createdKafkaConsumer._serviceProvider.CreateScope();
    try
    {
      AuthDbContext db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
      ICryptoService cryptoService = scope.ServiceProvider.GetRequiredService<ICryptoService>();
      IPhoneUtil phoneUtil = scope.ServiceProvider.GetRequiredService<IPhoneUtil>();
      string normalizedPhone;
      string phoneHash;
      if (await db.AuthKnownUsers.AnyAsync<AuthKnownUserEntity>((Expression<Func<AuthKnownUserEntity, bool>>) (u => u.UserId == message.UserId), cancellationToken))
      {
        createdKafkaConsumer._logger.LogDebug("User {UserId} already exists in AuthKnownUsers, skipping", (object) message.UserId);
        scope = (IServiceScope) null;
        db = (AuthDbContext) null;
        cryptoService = (ICryptoService) null;
        phoneUtil = (IPhoneUtil) null;
        normalizedPhone = (string) null;
        phoneHash = (string) null;
      }
      else
      {
        normalizedPhone = phoneUtil.NormalizeToE164(message.Phone);
        phoneHash = cryptoService.HashPhone(normalizedPhone);
        EntityEntry<AuthKnownUserEntity> entityEntry = await db.AuthKnownUsers.AddAsync(new AuthKnownUserEntity()
        {
          UserId = message.UserId,
          PhoneHash = phoneHash,
          IsActive = message.Status == "Active",
          CreatedAt = message.CreatedAt,
          UpdatedAt = DateTime.UtcNow
        }, cancellationToken);
        int num = await db.SaveChangesAsync(cancellationToken);
        createdKafkaConsumer._logger.LogInformation("User {UserId} added to AuthKnownUsers (PhoneHash={PhoneHash}, Status={Status})", (object) message.UserId, (object) phoneHash, (object) message.Status);
        scope = (IServiceScope) null;
        db = (AuthDbContext) null;
        cryptoService = (ICryptoService) null;
        phoneUtil = (IPhoneUtil) null;
        normalizedPhone = (string) null;
        phoneHash = (string) null;
      }
    }
    finally
    {
      scope?.Dispose();
    }
  }
}
