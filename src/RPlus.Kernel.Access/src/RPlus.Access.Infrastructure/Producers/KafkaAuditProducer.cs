// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Producers.KafkaAuditProducer
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Access.Application.Events.Integration;
using RPlus.Access.Application.Interfaces;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Producers;

public class KafkaAuditProducer : IAuditProducer
{
  public KafkaAuditProducer(
    IOptions<KafkaOptions> options,
    ILogger<KafkaAuditProducer> logger)
  {
      // Dummy implementation to unblock build
  }

  public async Task ProduceAuditLogAsync(AccessDecisionMadeEvent @event)
  {
    // TODO: Implement using MassTransit or new EventPublisher
    await Task.CompletedTask;
  }
}
