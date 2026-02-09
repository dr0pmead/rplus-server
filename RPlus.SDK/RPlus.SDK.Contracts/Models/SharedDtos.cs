// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Contracts.Models.CapabilityToken
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Contracts.Models;

public record CapabilityToken
{
  public required string Issuer { get; init; }

  public required DateTimeOffset IssuedAt { get; init; }

  public required string Signature { get; init; }

  public required Dictionary<string, string> Claims { get; init; }

  public CapabilityToken()
  {
  }
}
