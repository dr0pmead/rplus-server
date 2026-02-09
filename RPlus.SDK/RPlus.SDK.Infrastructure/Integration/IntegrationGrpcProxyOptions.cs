// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Integration.IntegrationGrpcProxyOptions
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Infrastructure.Integration;

public sealed class IntegrationGrpcProxyOptions
{
  public Dictionary<string, string> Services { get; init; } = new Dictionary<string, string>();
}
