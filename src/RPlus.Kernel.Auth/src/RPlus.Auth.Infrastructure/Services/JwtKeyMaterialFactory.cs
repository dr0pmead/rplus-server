// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.JwtKeyMaterialFactory
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.IdentityModel.Tokens;
using RPlus.Auth.Application.Models;
using System;
using System.Security.Cryptography;
using System.Text;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

internal static class JwtKeyMaterialFactory
{
  public static JwtKeyMaterial CreateFromPrivatePem(
    string privatePem,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt)
  {
    using (RSA rsa = RSA.Create())
    {
      rsa.ImportFromPem(JwtKeyMaterialFactory.NormalizePem(privatePem).AsSpan());
      string PublicKeyPem = rsa.ExportRSAPublicKeyPem();
      return new JwtKeyMaterial(JwtKeyMaterialFactory.ComputeKeyId(rsa.ExportParameters(false)), privatePem, PublicKeyPem, createdAt, expiresAt);
    }
  }

  public static JwtKeyMaterial GenerateNew(
    int keySize,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt)
  {
    using (RSA rsa = RSA.Create(keySize))
    {
      string PrivateKeyPem = rsa.ExportRSAPrivateKeyPem();
      string PublicKeyPem = rsa.ExportRSAPublicKeyPem();
      return new JwtKeyMaterial(JwtKeyMaterialFactory.ComputeKeyId(rsa.ExportParameters(false)), PrivateKeyPem, PublicKeyPem, createdAt, expiresAt);
    }
  }

  private static string NormalizePem(string value)
  {
    return value.Contains("BEGIN", StringComparison.Ordinal) ? value : Encoding.UTF8.GetString(Convert.FromBase64String(value));
  }

  private static string ComputeKeyId(RSAParameters parameters)
  {
    string s = $"{Base64UrlEncoder.Encode(parameters.Modulus ?? Array.Empty<byte>())}.{Base64UrlEncoder.Encode(parameters.Exponent ?? Array.Empty<byte>())}";
    using (SHA256 shA256 = SHA256.Create())
      return Base64UrlEncoder.Encode(shA256.ComputeHash(Encoding.ASCII.GetBytes(s)));
  }
}
