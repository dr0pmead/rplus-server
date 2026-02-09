// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.IntegrationStatsPublisher
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using RPlus.Core.Kafka;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Infrastructure.Integration;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationStatsPublisher : IIntegrationStatsPublisher
{
  private const string Topic = "kernel.integration.stats.v1";
  private readonly IKafkaProducer<string, IntegrationStatsEvent> _producer;

  public IntegrationStatsPublisher(
    IKafkaProducer<string, IntegrationStatsEvent> producer)
  {
    this._producer = producer;
  }

  public Task PublishAsync(IntegrationStatsEvent statsEvent, CancellationToken cancellationToken)
  {
    return this._producer.ProduceAsync("kernel.integration.stats.v1", statsEvent.KeyId.ToString(), statsEvent, cancellationToken);
  }
}
