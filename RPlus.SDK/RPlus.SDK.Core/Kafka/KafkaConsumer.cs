// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Kafka.KafkaConsumer`1
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Options;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Core.Kafka;

public abstract class KafkaConsumer<TValue>(
  IOptions<KafkaOptions> options,
  string topic,
  string groupId,
  ILogger logger) : KafkaConsumerBackgroundService<string, TValue>(KafkaConsumer<TValue>.OverrideGroupId(options, groupId), logger, topic)
{
  private static IOptions<KafkaOptions> OverrideGroupId(
    IOptions<KafkaOptions> options,
    string groupId)
  {
    return Microsoft.Extensions.Options.Options.Create<KafkaOptions>(new KafkaOptions()
    {
      BootstrapServers = options.Value.BootstrapServers,
      GroupId = groupId,
      SessionTimeoutSeconds = options.Value.SessionTimeoutSeconds
    });
  }

  protected override Task HandleMessageAsync(
    string key,
    TValue message,
    CancellationToken cancellationToken)
  {
    return this.HandleAsync(message, cancellationToken);
  }

  protected abstract Task HandleAsync(TValue message, CancellationToken ct);
}
