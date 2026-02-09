// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Integration.ApiKeyMetadata
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Infrastructure.Integration;

public sealed record ApiKeyMetadata
{
  public required Guid KeyId { get; init; }

  public required Guid? PartnerId { get; init; }

  public required string Env { get; init; }

  public required string Prefix { get; init; }

  public required string Secret { get; init; }

  public required IReadOnlyCollection<string> Scopes { get; init; }

  public required IReadOnlyDictionary<string, int> RateLimits { get; init; }

  public DateTimeOffset? ExpiresAt { get; init; }

  public bool RequireSignature { get; init; }

  public bool IsActive { get; init; }

  public int DailyQuota { get; init; }
}
