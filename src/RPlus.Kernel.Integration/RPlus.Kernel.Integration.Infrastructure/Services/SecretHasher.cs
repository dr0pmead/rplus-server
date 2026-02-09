// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.SecretHasher
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using System;
using System.Security.Cryptography;
using System.Text;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public static class SecretHasher
{
  public static string Hash(string secret)
  {
    using (SHA256 shA256 = SHA256.Create())
      return Convert.ToHexString(shA256.ComputeHash(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
  }
}
