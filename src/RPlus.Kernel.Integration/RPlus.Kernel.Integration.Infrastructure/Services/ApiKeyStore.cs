// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.ApiKeyStore
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Domain.ValueObjects;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using RPlus.SDK.Infrastructure.Integration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class ApiKeyStore : IApiKeyStore
{
  private readonly IntegrationDbContext _db;
  private readonly IConnectionMultiplexer _redis;
  private readonly ISecretProtector _protector;
  private readonly TimeSpan _ttl;

  public ApiKeyStore(
    IntegrationDbContext db,
    IConnectionMultiplexer redis,
    ISecretProtector protector,
    TimeSpan? ttl = null)
  {
    this._db = db;
    this._redis = redis;
    this._protector = protector;
    this._ttl = ttl ?? TimeSpan.FromMinutes(10L);
  }

  public async Task<ApiKeyMetadata?> GetBySecretAsync(
    string secret,
    string env,
    CancellationToken cancellationToken)
  {
    string hash = SecretHasher.Hash(secret);
    string normalizedEnv = env?.ToLowerInvariant() ?? string.Empty;
    IDatabase db = this._redis.GetDatabase();
    RedisValue async = await db.StringGetAsync((RedisKey) ("sys:integ:key:secret:" + hash));
    Guid result;
    if (!async.IsNullOrEmpty && Guid.TryParse(async.ToString(), out result))
    {
      ApiKeyMetadata cachedMetadataAsync = await this.GetCachedMetadataAsync(db, result);
      if ((object) cachedMetadataAsync != null)
        return cachedMetadataAsync;
    }
    IntegrationApiKey integrationApiKey = await this._db.ApiKeys.AsNoTracking<IntegrationApiKey>().FirstOrDefaultAsync<IntegrationApiKey>((Expression<Func<IntegrationApiKey, bool>>) (k => k.KeyHash == hash && k.Environment.ToLower() == normalizedEnv), cancellationToken);
    if (integrationApiKey == null)
      return (ApiKeyMetadata) null;
    ApiKeyMetadata metadata = new ApiKeyMetadata()
    {
      KeyId = integrationApiKey.Id,
      PartnerId = integrationApiKey.PartnerId,
      Env = normalizedEnv,
      Prefix = integrationApiKey.Prefix,
      Secret = this._protector.Unprotect(integrationApiKey.SecretProtected),
      Scopes = (IReadOnlyCollection<string>) (integrationApiKey.Scopes ?? new List<string>()),
      RateLimits = (IReadOnlyDictionary<string, int>) (integrationApiKey.RateLimits ?? new Dictionary<string, int>()),
      ExpiresAt = integrationApiKey.ExpiresAt.HasValue ? new DateTimeOffset?(new DateTimeOffset(integrationApiKey.ExpiresAt.Value)) : new DateTimeOffset?(),
      RequireSignature = integrationApiKey.RequireSignature,
      IsActive = integrationApiKey.Status == "Active",
      DailyQuota = integrationApiKey.RateLimits != null && integrationApiKey.RateLimits.TryGetValue("daily", out int daily) ? daily : 0
    };
    await this.CacheMetadataAsync(db, hash, metadata);
    return metadata;
  }

  public async Task RevokeAsync(Guid keyId, CancellationToken cancellationToken)
  {
    IntegrationApiKey apiKey = await this._db.ApiKeys.FirstOrDefaultAsync<IntegrationApiKey>((Expression<Func<IntegrationApiKey, bool>>) (k => k.Id == keyId), cancellationToken);
    IDatabase db;
    if (apiKey == null)
    {
      apiKey = (IntegrationApiKey) null;
      db = (IDatabase) null;
    }
    else
    {
      apiKey.Revoke();
      int num1 = await this._db.SaveChangesAsync(cancellationToken);
      db = this._redis.GetDatabase();
      int num2 = await db.KeyDeleteAsync((RedisKey) $"sys:integ:key:{keyId}") ? 1 : 0;
      int num3 = await db.KeyDeleteAsync((RedisKey) ("sys:integ:key:secret:" + apiKey.KeyHash)) ? 1 : 0;
      apiKey = (IntegrationApiKey) null;
      db = (IDatabase) null;
    }
  }

  private async Task<ApiKeyMetadata?> GetCachedMetadataAsync(IDatabase db, Guid keyId)
  {
    RedisValue async = await db.StringGetAsync((RedisKey) $"sys:integ:key:{keyId}");
    return !async.IsNullOrEmpty ? JsonSerializer.Deserialize<ApiKeyMetadata>(async.ToString()) : (ApiKeyMetadata) null;
  }

  private async Task CacheMetadataAsync(IDatabase db, string secretHash, ApiKeyMetadata metadata)
  {
    string str = JsonSerializer.Serialize<ApiKeyMetadata>(metadata);
    int num1 = await db.StringSetAsync((RedisKey) $"sys:integ:key:{metadata.KeyId}", (RedisValue) str, new TimeSpan?(this._ttl), false) ? 1 : 0;
    int num2 = await db.StringSetAsync((RedisKey) ("sys:integ:key:secret:" + secretHash), (RedisValue) metadata.KeyId.ToString(), new TimeSpan?(this._ttl), false) ? 1 : 0;
  }

  private static ApiKeyEnvironment ParseEnv(string env)
  {
    return !env.Equals("test", StringComparison.OrdinalIgnoreCase) ? ApiKeyEnvironment.Live : ApiKeyEnvironment.Test;
  }
}
