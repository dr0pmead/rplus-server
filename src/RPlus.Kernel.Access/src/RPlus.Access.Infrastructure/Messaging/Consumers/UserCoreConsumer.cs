// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Messaging.Consumers.UserCoreConsumer
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces.Monitoring;
using RPlus.Core.Contracts.Events;
using StackExchange.Redis;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Messaging.Consumers;

public class UserCoreConsumer(
  IConnectionMultiplexer redis,
  ILogger<UserCoreConsumer> logger,
  IAccessMetrics metrics) : IdempotentConsumer<UserCoreEvent>(redis, (ILogger) logger, metrics)
{
  protected override string GetDedupKey(UserCoreEvent message)
  {
    return $"user_core:{message.UserId}:{message.UpdatedAt.Ticks}";
  }

  protected override Task HandleAsync(UserCoreEvent message, CancellationToken ct)
  {
    this._logger.LogInformation("Processing UserCore update for {UserId}: Status={Status}, Validating caches...", (object) message.UserId, (object) message.Status);
    return Task.CompletedTask;
  }
}
