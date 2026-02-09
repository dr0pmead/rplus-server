// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.RuntimeRequirements
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System.Text.Json.Serialization;

#nullable disable
namespace RPlus.SDK.Core.Abstractions;

public sealed class RuntimeRequirements
{
  [JsonPropertyName("DB")]
  public bool Db { get; set; }

  public bool Kafka { get; set; }

  public bool Scheduler { get; set; }

  public bool Http { get; set; }
}
