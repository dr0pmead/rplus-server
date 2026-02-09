// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Infrastructure.Messaging.Consumers.UserCreatedConsumer
// Assembly: RPlus.Organization.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67956CC4-52BB-48F3-9302-33FB247F5EB1
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.SDK.Core.Messaging.Events;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Organization.Infrastructure.Messaging.Consumers;

public class UserCreatedConsumer(
  IOptions<KafkaOptions> options,
  ILogger<UserCreatedConsumer> logger) : KafkaConsumer<UserCreated>(options, "user.identity.v1", "rplus-organization-service-group", (ILogger) logger)
{
  protected override Task HandleAsync(UserCreated message, CancellationToken ct)
  {
    this._logger.LogInformation("Organization Service received UserCreated event for {UserId}. Ready to link to Organization.", (object) message.UserId);
    return Task.CompletedTask;
  }
}
