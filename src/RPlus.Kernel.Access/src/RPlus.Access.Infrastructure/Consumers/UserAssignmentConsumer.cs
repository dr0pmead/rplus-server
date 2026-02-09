// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Consumers.UserAssignmentConsumer
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Access.Application.Commands.UpdateLocalAssignments;
using RPlus.Access.Application.Events.Integration;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using StackExchange.Redis;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Consumers;

public class UserAssignmentConsumer : 
  KafkaConsumerBackgroundService<string, UserAssignmentCreatedEvent>
{
  private readonly IServiceScopeFactory _scopeFactory;

  public UserAssignmentConsumer(
    IOptions<KafkaOptions> options,
    ILogger<UserAssignmentConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis)
    : base(options, (ILogger) logger, "org.assignment.created")
  {
    this._scopeFactory = scopeFactory;
  }

  protected override async Task HandleMessageAsync(
    string key,
    UserAssignmentCreatedEvent @event,
    CancellationToken cancellationToken)
  {
    using (IServiceScope scope = this._scopeFactory.CreateScope())
      await scope.ServiceProvider.GetRequiredService<IMediator>().Send<UpdateLocalUserAssignmentCommand>(new UpdateLocalUserAssignmentCommand(@event.UserId, @event.NodeId, @event.RoleCode, @event.PathSnapshot), cancellationToken);
  }
}
