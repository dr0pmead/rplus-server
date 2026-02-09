// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.CryptoService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Options;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public class CryptoService : ICryptoService
{
  private readonly CryptoOptions _options;

  public CryptoService(IOptions<CryptoOptions> options) => this._options = options.Value;

  public string HashPhone(string phone)
  {
    using (SHA256 shA256 = SHA256.Create())
    {
      byte[] bytes = Encoding.UTF8.GetBytes(phone + this._options.PhoneHashingSalt);
      return Convert.ToHexString(shA256.ComputeHash(bytes));
    }
  }

  public string ComputeOtpHash(string phone, string code, DateTime created)
  {
    using (HMACSHA256 hmacshA256 = new HMACSHA256(Encoding.UTF8.GetBytes(this._options.OtpSigningKey)))
    {
      long unixTimeSeconds = new DateTimeOffset(created).ToUnixTimeSeconds();
      string s = $"{phone}:{code}:{unixTimeSeconds}";
      return Convert.ToHexString(hmacshA256.ComputeHash(Encoding.UTF8.GetBytes(s)));
    }
  }

  public Task<string> EncryptPhoneAsync(string phone, CancellationToken cancellationToken)
  {
    return Task.FromResult<string>(Convert.ToBase64String(Encoding.UTF8.GetBytes(phone)));
  }

  public Task<string> DecryptPhoneAsync(
    string encryptedPayload,
    CancellationToken cancellationToken)
  {
    try
    {
      return Task.FromResult<string>(Encoding.UTF8.GetString(Convert.FromBase64String(encryptedPayload)));
    }
    catch
    {
      return Task.FromResult<string>(encryptedPayload);
    }
  }

  public string GenerateSecureToken(int byteLength = 32 /*0x20*/)
  {
    return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));
  }

  public string HashToken(string token)
  {
    using (SHA256 shA256 = SHA256.Create())
      return Convert.ToHexString(shA256.ComputeHash(Encoding.UTF8.GetBytes(token)));
  }

  public string HashRefreshSecret(string secret) => this.HashToken(secret);

  public bool VerifyRefreshSecret(string secret, string hash)
  {
    return CryptographicOperations.FixedTimeEquals((ReadOnlySpan<byte>) Convert.FromHexString(this.HashRefreshSecret(secret)), (ReadOnlySpan<byte>) Convert.FromHexString(hash));
  }

  public byte[] GenerateSalt(int length = 16 /*0x10*/) => RandomNumberGenerator.GetBytes(length);

  public byte[] HashPassword(string password, byte[] salt)
  {
    using (Argon2id argon2id = new Argon2id(Encoding.UTF8.GetBytes(password)))
    {
      argon2id.Salt = salt;
      argon2id.DegreeOfParallelism = 4;
      argon2id.MemorySize = 65536 /*0x010000*/;
      argon2id.Iterations = 3;
      return argon2id.GetBytes(32 /*0x20*/);
    }
  }

  public bool VerifyPassword(string password, byte[] hash, byte[] salt)
  {
    return CryptographicOperations.FixedTimeEquals((ReadOnlySpan<byte>) this.HashPassword(password, salt), (ReadOnlySpan<byte>) hash);
  }
}
