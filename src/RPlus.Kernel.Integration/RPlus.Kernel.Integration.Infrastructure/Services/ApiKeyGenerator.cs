// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.ApiKeyGenerator
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using RPlus.Kernel.Integration.Domain.ValueObjects;
using System;
using System.Security.Cryptography;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

internal static class ApiKeyGenerator
{
  public static string GenerateSecret()
  {
    byte[] numArray = new byte[32 /*0x20*/];
    RandomNumberGenerator.Fill((Span<byte>) numArray);
    return ApiKeyGenerator.Base64UrlEncode(numArray);
  }

  public static string BuildPrefix(string env)
  {
    return $"rp_{(env.Equals("test", StringComparison.OrdinalIgnoreCase) ? "test" : "live")}_v1_sk_";
  }

  public static ApiKeyEnvironment ParseEnv(string env)
  {
    return !env.Equals("test", StringComparison.OrdinalIgnoreCase) ? ApiKeyEnvironment.Live : ApiKeyEnvironment.Test;
  }

  public static bool IsValidEnv(string env)
  {
    return env.Equals("test", StringComparison.OrdinalIgnoreCase) || env.Equals("live", StringComparison.OrdinalIgnoreCase);
  }

  private static string Base64UrlEncode(byte[] input)
  {
    return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }
}
