// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.JwtKeyProvider
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Application.Models;
using RPlus.Auth.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class JwtKeyProvider : IJwtKeyProvider
{
  private readonly IOptionsMonitor<JwtOptions> _options;
  private readonly IJwtKeyStore _store;
  private readonly object _lock = new object();
  private SigningCredentials? _cachedSigning;
  private IReadOnlyList<RsaSecurityKey> _cachedPublicKeys = (IReadOnlyList<RsaSecurityKey>) Array.Empty<RsaSecurityKey>();
  private string? _cachedKeyId;
  private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
  private TimeSpan _cacheInterval = TimeSpan.FromSeconds(30L);

  public JwtKeyProvider(IOptionsMonitor<JwtOptions> options, IJwtKeyStore store)
  {
    this._options = options;
    this._store = store;
  }

  public SigningCredentials GetSigningCredentials()
  {
    this.EnsureKeys();
    return this._cachedSigning;
  }

  public RsaSecurityKey GetPublicKey()
  {
    this.EnsureKeys();
    return this._cachedPublicKeys.First<RsaSecurityKey>();
  }

  public IReadOnlyList<RsaSecurityKey> GetPublicKeys()
  {
    this.EnsureKeys();
    return this._cachedPublicKeys;
  }

  public string GetKeyId()
  {
    this.EnsureKeys();
    return this._cachedKeyId;
  }

  private void EnsureKeys()
  {
    lock (this._lock)
    {
      DateTimeOffset utcNow = DateTimeOffset.UtcNow;
      this._cacheInterval = TimeSpan.FromSeconds((long) Math.Max(5, this._options.CurrentValue.KeyCacheSeconds));
      if (this._cachedSigning != null && this._cachedPublicKeys.Count > 0 && !string.IsNullOrWhiteSpace(this._cachedKeyId) && utcNow - this._lastRefresh < this._cacheInterval)
        return;
      JwtOptions currentValue = this._options.CurrentValue;
      if (!string.IsNullOrWhiteSpace(currentValue.PrivateKeyPem) && !currentValue.PrivateKeyPem.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase))
      {
        string privateKeyPem = currentValue.PrivateKeyPem;
        string publicKeyPem = currentValue.PublicKeyPem;
        if (publicKeyPem == null)
          throw new InvalidOperationException("PublicKeyPem required when PrivateKeyPem is set");
        DateTimeOffset minValue = DateTimeOffset.MinValue;
        DateTimeOffset maxValue = DateTimeOffset.MaxValue;
        JwtKeyMaterial material = new JwtKeyMaterial("static-key", privateKeyPem, publicKeyPem, minValue, maxValue);
        this._cachedPublicKeys = (IReadOnlyList<RsaSecurityKey>) new RsaSecurityKey[1]
        {
          JwtKeyProvider.CreatePublicKey(material)
        };
        this._cachedSigning = JwtKeyProvider.CreateSigningCredentials(material);
        this._cachedKeyId = material.KeyId;
        this._lastRefresh = utcNow;
      }
      else
      {
        JwtKeyMaterial activeKey = this._store.GetActiveKey();
        if (activeKey == (JwtKeyMaterial) null)
          throw new InvalidOperationException("JWT active key is not initialized.");
        IReadOnlyList<JwtKeyMaterial> allKeys = this._store.GetAllKeys();
        List<RsaSecurityKey> rsaSecurityKeyList = new List<RsaSecurityKey>(allKeys.Count);
        foreach (JwtKeyMaterial material in (IEnumerable<JwtKeyMaterial>) allKeys)
          rsaSecurityKeyList.Add(JwtKeyProvider.CreatePublicKey(material));
        this._cachedPublicKeys = (IReadOnlyList<RsaSecurityKey>) rsaSecurityKeyList;
        this._cachedSigning = JwtKeyProvider.CreateSigningCredentials(activeKey);
        this._cachedKeyId = activeKey.KeyId;
        this._lastRefresh = utcNow;
      }
    }
  }

  private static SigningCredentials CreateSigningCredentials(JwtKeyMaterial material)
  {
    string str = JwtKeyProvider.NormalizePem(material.PrivateKeyPem);
    RSA rsa = RSA.Create();
    try
    {
      rsa.ImportFromPem(str.AsSpan());
    }
    catch (Exception ex)
    {
      string base64Body = JwtKeyProvider.GetBase64Body(str);
      int bytesRead;
      if (!string.IsNullOrWhiteSpace(base64Body))
      {
        try
        {
          rsa.ImportRSAPrivateKey((ReadOnlySpan<byte>) Convert.FromBase64String(base64Body), out bytesRead);
        }
        catch
        {
          rsa.ImportPkcs8PrivateKey((ReadOnlySpan<byte>) Convert.FromBase64String(base64Body), out bytesRead);
        }
      }
      else
        throw;
    }
    RsaSecurityKey key = new RsaSecurityKey(rsa);
    key.KeyId = material.KeyId;
    return new SigningCredentials((SecurityKey) key, "RS256");
  }

  private static RsaSecurityKey CreatePublicKey(JwtKeyMaterial material)
  {
    string str = JwtKeyProvider.NormalizePem(material.PublicKeyPem);
    RSA rsa = RSA.Create();
    try
    {
      rsa.ImportFromPem(str.AsSpan());
    }
    catch (Exception ex)
    {
      string base64Body = JwtKeyProvider.GetBase64Body(str);
      int bytesRead;
      if (!string.IsNullOrWhiteSpace(base64Body))
      {
        try
        {
          rsa.ImportRSAPublicKey((ReadOnlySpan<byte>) Convert.FromBase64String(base64Body), out bytesRead);
        }
        catch
        {
          rsa.ImportSubjectPublicKeyInfo((ReadOnlySpan<byte>) Convert.FromBase64String(base64Body), out bytesRead);
        }
      }
      else
        throw;
    }
    RsaSecurityKey publicKey = new RsaSecurityKey(rsa);
    publicKey.KeyId = material.KeyId;
    return publicKey;
  }

  private static string NormalizePem(string value)
  {
    return value.Contains("BEGIN", StringComparison.Ordinal) ? value : Encoding.UTF8.GetString(Convert.FromBase64String(value));
  }

  private static string GetBase64Body(string pem)
  {
    return Regex.Replace(Regex.Replace(pem, "-{5}.*?-{5}", ""), "\\s+", "");
  }
}
