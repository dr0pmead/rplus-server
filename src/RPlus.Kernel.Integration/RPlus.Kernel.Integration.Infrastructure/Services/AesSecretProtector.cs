// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.AesSecretProtector
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using System;
using System.Security.Cryptography;
using System.Text;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class AesSecretProtector : ISecretProtector
{
  private const int NonceSize = 12;
  private const int TagSize = 16 /*0x10*/;
  private readonly byte[] _key;

  public AesSecretProtector(string secretKey)
  {
    this._key = SHA256.HashData(Encoding.UTF8.GetBytes(secretKey));
  }

  public string Protect(string plaintext)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(plaintext);
    byte[] numArray1 = new byte[12];
    RandomNumberGenerator.Fill((Span<byte>) numArray1);
    byte[] numArray2 = new byte[bytes.Length];
    byte[] numArray3 = new byte[16 /*0x10*/];
    using (AesGcm aesGcm = new AesGcm(this._key, 16 /*0x10*/))
    {
      aesGcm.Encrypt(numArray1, bytes, numArray2, numArray3);
      byte[] numArray4 = new byte[28 + numArray2.Length];
      Buffer.BlockCopy((Array) numArray1, 0, (Array) numArray4, 0, 12);
      Buffer.BlockCopy((Array) numArray3, 0, (Array) numArray4, 12, 16 /*0x10*/);
      Buffer.BlockCopy((Array) numArray2, 0, (Array) numArray4, 28, numArray2.Length);
      return Convert.ToBase64String(numArray4);
    }
  }

  public string Unprotect(string protectedValue)
  {
    byte[] src = Convert.FromBase64String(protectedValue);
    if (src.Length < 28)
      throw new InvalidOperationException("Invalid protected payload.");
    byte[] numArray1 = new byte[12];
    byte[] numArray2 = new byte[16 /*0x10*/];
    byte[] numArray3 = new byte[src.Length - 12 - 16 /*0x10*/];
    Buffer.BlockCopy((Array) src, 0, (Array) numArray1, 0, 12);
    Buffer.BlockCopy((Array) src, 12, (Array) numArray2, 0, 16 /*0x10*/);
    Buffer.BlockCopy((Array) src, 28, (Array) numArray3, 0, numArray3.Length);
    byte[] numArray4 = new byte[numArray3.Length];
    using (AesGcm aesGcm = new AesGcm(this._key, 16 /*0x10*/))
    {
      aesGcm.Decrypt(numArray1, numArray3, numArray2, numArray4);
      return Encoding.UTF8.GetString(numArray4);
    }
  }
}
