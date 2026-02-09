// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Kafka.IKafkaProducer`2
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Core.Kafka;

public interface IKafkaProducer<TKey, TValue>
{
  Task ProduceAsync(string topic, TKey key, TValue value, CancellationToken cancellationToken = default (CancellationToken));
}

public interface IKafkaConsumer<TMessage>
{
  Task ConsumeAsync(TMessage message, CancellationToken cancellationToken);
}
