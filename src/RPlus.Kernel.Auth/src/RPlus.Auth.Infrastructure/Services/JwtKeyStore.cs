// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.JwtKeyStore
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Application.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class JwtKeyStore : IJwtKeyStore
{
  private const string KeysSetKey = "auth:jwt:keys";
  private const string ActiveKeyKey = "auth:jwt:active";
  private const string KeyPrefix = "auth:jwt:key:";
  private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
  private readonly IDatabase _db;
  private readonly IDataProtector _protector;
  private readonly ILogger<JwtKeyStore> _logger;

  public JwtKeyStore(
    IConnectionMultiplexer redis,
    IDataProtectionProvider protectionProvider,
    ILogger<JwtKeyStore> logger)
  {
    this._db = redis.GetDatabase();
    this._protector = protectionProvider.CreateProtector("rplus-auth-jwt-key-store");
    this._logger = logger;
  }

  public JwtKeyMaterial? GetActiveKey()
  {
    RedisValue kid = this._db.StringGet((RedisKey) "auth:jwt:active");
    return kid.IsNullOrEmpty ? (JwtKeyMaterial) null : this.GetKey(kid);
  }

  public IReadOnlyList<JwtKeyMaterial> GetAllKeys()
  {
    RedisValue[] redisValueArray = this._db.SetMembers((RedisKey) "auth:jwt:keys");
    if (redisValueArray.Length == 0)
      return (IReadOnlyList<JwtKeyMaterial>) Array.Empty<JwtKeyMaterial>();
    DateTimeOffset utcNow = DateTimeOffset.UtcNow;
    List<JwtKeyMaterial> allKeys = new List<JwtKeyMaterial>(redisValueArray.Length);
    foreach (RedisValue kid in redisValueArray)
    {
      JwtKeyMaterial key = this.GetKey(kid);
      if (key == (JwtKeyMaterial) null)
        this._db.SetRemove((RedisKey) "auth:jwt:keys", kid);
      else if (key.ExpiresAt <= utcNow)
        this._db.SetRemove((RedisKey) "auth:jwt:keys", kid);
      else
        allKeys.Add(key);
    }
    return (IReadOnlyList<JwtKeyMaterial>) allKeys;
  }

  public void SaveActiveKey(JwtKeyMaterial material)
  {
    this.SaveKey(material);
    this._db.StringSet((RedisKey) "auth:jwt:active", (RedisValue) material.KeyId);
  }

  public void CleanupExpiredKeys() => this.GetAllKeys();

  private void SaveKey(JwtKeyMaterial material)
  {
    string PrivateKeyProtected = this._protector.Protect(material.PrivateKeyPem);
    string str = JsonSerializer.Serialize<JwtKeyStore.JwtKeyPayload>(new JwtKeyStore.JwtKeyPayload(material.KeyId, PrivateKeyProtected, material.PublicKeyPem, material.CreatedAt, material.ExpiresAt), JwtKeyStore.JsonOptions);
    TimeSpan timeSpan = material.ExpiresAt - DateTimeOffset.UtcNow;
    if (timeSpan <= TimeSpan.Zero)
      return;
    this._db.StringSet((RedisKey) ("auth:jwt:key:" + material.KeyId), (RedisValue) str, timeSpan);
    this._db.SetAdd((RedisKey) "auth:jwt:keys", (RedisValue) material.KeyId);
  }

  private JwtKeyMaterial? GetKey(RedisValue kid)
  {
    if (kid.IsNullOrEmpty)
      return (JwtKeyMaterial) null;
    RedisValue redisValue = this._db.StringGet((RedisKey) ("auth:jwt:key:" + (string) kid));
    if (redisValue.IsNullOrEmpty)
      return (JwtKeyMaterial) null;
    try
    {
      JwtKeyStore.JwtKeyPayload jwtKeyPayload = JsonSerializer.Deserialize<JwtKeyStore.JwtKeyPayload>(redisValue.ToString(), JwtKeyStore.JsonOptions);
      if (jwtKeyPayload == (JwtKeyStore.JwtKeyPayload) null)
        return (JwtKeyMaterial) null;
      string PrivateKeyPem = this._protector.Unprotect(jwtKeyPayload.PrivateKeyProtected);
      return new JwtKeyMaterial(jwtKeyPayload.KeyId, PrivateKeyPem, jwtKeyPayload.PublicKeyPem, jwtKeyPayload.CreatedAt, jwtKeyPayload.ExpiresAt);
    }
    catch (Exception ex)
    {
      this._logger.LogWarning(ex, "Failed to decode JWT key {KeyId}", (object) kid.ToString());
      return (JwtKeyMaterial) null;
    }
  }

  private sealed record JwtKeyPayload(
    string KeyId,
    string PrivateKeyProtected,
    string PublicKeyPem,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
  ;
}
