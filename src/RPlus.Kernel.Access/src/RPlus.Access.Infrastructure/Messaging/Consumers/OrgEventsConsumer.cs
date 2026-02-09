// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Messaging.Consumers.OrgEventsConsumer
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces.Monitoring;
using RPlus.Access.Infrastructure.Messaging.Events;
using StackExchange.Redis;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Messaging.Consumers;

public class OrgEventsConsumer(
  IConnectionMultiplexer redis,
  ILogger<OrgEventsConsumer> logger,
  IAccessMetrics metrics) : IdempotentConsumer<NodeMovedEvent>(redis, (ILogger) logger, metrics)
{
  protected override string GetDedupKey(NodeMovedEvent message)
  {
    return $"node_moved:{message.NodeId}:{message.NewPath}";
  }

  protected override Task HandleAsync(NodeMovedEvent message, CancellationToken ct)
  {
    this._logger.LogInformation("Processing Node Moved: {NodeId} from {Old} to {New}", (object) message.NodeId, (object) message.OldPath, (object) message.NewPath);
    return Task.CompletedTask;
  }
}
