// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Services.RateLimitExceededException
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using System;

#nullable disable
namespace RPlus.Auth.Services;

public sealed class RateLimitExceededException : Exception
{
  public RateLimitExceededException(TimeSpan retryAfter)
    : base("Rate limit exceeded.")
  {
    this.RetryAfter = retryAfter;
  }

  public TimeSpan RetryAfter { get; }
}
