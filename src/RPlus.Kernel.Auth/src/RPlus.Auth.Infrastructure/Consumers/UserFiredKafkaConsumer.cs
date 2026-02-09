// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Consumers.UserFiredKafkaConsumer
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Contracts.Events;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Consumers;

public class UserFiredKafkaConsumer : KafkaConsumerBackgroundService<string, UserFiredEvent>
{
  private readonly IConnectionMultiplexer _redisConnection;

  public UserFiredKafkaConsumer(
    IOptions<KafkaOptions> options,
    IConnectionMultiplexer redis,
    ILogger<UserFiredKafkaConsumer> logger)
    : base(options, (ILogger) logger, "user-fired")
  {
    this._redisConnection = redis;
  }

  protected override async Task HandleMessageAsync(
    string key,
    UserFiredEvent message,
    CancellationToken cancellationToken)
  {
    UserFiredKafkaConsumer firedKafkaConsumer = this;
    Guid userId = message.UserId;
    int num = await firedKafkaConsumer._redisConnection.GetDatabase().StringSetAsync((RedisKey) $"blocked:userid:{userId}", (RedisValue) ("fired: " + message.Reason), TimeSpan.FromMinutes(20)) ? 1 : 0;
    firedKafkaConsumer._logger.LogWarning("User {UserId} fired. Instant block activated in Redis (via Kafka).", (object) userId);
  }
}
