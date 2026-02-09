// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Options.KafkaOptions
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

#nullable enable
namespace RPlus.Core.Options;

public sealed class KafkaOptions
{
  public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "kernel-kafka:9092";

  public string GroupId { get; set; } = "rplus-default";

  public int? SessionTimeoutSeconds { get; set; }
}
