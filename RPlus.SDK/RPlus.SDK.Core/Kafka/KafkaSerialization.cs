// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Kafka.KafkaSerialization
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using Confluent.Kafka;
using System;
using System.Text.Json;

#nullable enable
namespace RPlus.Core.Kafka;

internal static class KafkaSerialization
{
  private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
  {
    PropertyNameCaseInsensitive = true
  };

  public static ISerializer<T> GetSerializer<T>()
  {
    return typeof (T) == typeof (string) ? (ISerializer<T>) Serializers.Utf8 : (ISerializer<T>) new KafkaSerialization.JsonKafkaSerializer<T>();
  }

  public static IDeserializer<T> GetDeserializer<T>()
  {
    return typeof (T) == typeof (string) ? (IDeserializer<T>) Deserializers.Utf8 : (IDeserializer<T>) new KafkaSerialization.JsonKafkaDeserializer<T>();
  }

  private sealed class JsonKafkaSerializer<T> : ISerializer<T>
  {
    public byte[] Serialize(T data, SerializationContext context)
    {
      return JsonSerializer.SerializeToUtf8Bytes<T>(data, KafkaSerialization.JsonOptions);
    }
  }

  private sealed class JsonKafkaDeserializer<T> : IDeserializer<T>
  {
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
      return isNull || data.IsEmpty
        ? default!
        : JsonSerializer.Deserialize<T>(data, KafkaSerialization.JsonOptions)!;
    }
  }
}
