using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationAdminService
{
  private readonly IntegrationDbContext _db;
  private readonly IConnectionMultiplexer _redis;
  private readonly ISecretProtector _protector;
  private readonly ILogger<IntegrationAdminService> _logger;

  public IntegrationAdminService(
    IntegrationDbContext db,
    IConnectionMultiplexer redis,
    ISecretProtector protector,
    ILogger<IntegrationAdminService> logger)
  {
    this._db = db;
    this._redis = redis;
    this._protector = protector;
    this._logger = logger;
  }

  public async Task<IntegrationPartner> CreatePartnerAsync(
    string name,
    string? description,
    CancellationToken cancellationToken)
  {
    IntegrationPartner partner = new IntegrationPartner(name, description ?? string.Empty, false);
    this._db.Partners.Add(partner);
    await this._db.SaveChangesAsync(cancellationToken);
    return partner;
  }

  public Task<List<IntegrationPartner>> GetPartnersAsync(CancellationToken cancellationToken)
  {
    return this._db.Partners.AsNoTracking<IntegrationPartner>()
      .OrderBy<IntegrationPartner, string>((Expression<Func<IntegrationPartner, string>>) (p => p.Name))
      .ToListAsync<IntegrationPartner>(cancellationToken);
  }

  /// <summary>
  /// Creates a new API key pair for a partner.
  /// Returns TWO independent cryptographic values:
  ///   - ApiKeyRaw:   sent in X-Integration-Key header (identification)
  ///   - HmacSecret:  used to compute X-Signature (signing, never transmitted)
  /// </summary>
  public async Task<(IntegrationApiKey ApiKey, string ApiKeyRaw, string HmacSecret)> CreateApiKeyAsync(
    Guid partnerId,
    string env,
    IEnumerable<string> scopes,
    IDictionary<string, int>? rateLimits,
    DateTime? expiresAt,
    bool requireSignature,
    CancellationToken cancellationToken)
  {
    var integrationPartner = await this._db.Partners
      .FirstOrDefaultAsync<IntegrationPartner>(p => p.Id == partnerId, cancellationToken);
    if (integrationPartner == null || !integrationPartner.IsActive)
      throw new InvalidOperationException("partner_not_found_or_inactive");

    bool hasKey = await this._db.ApiKeys.AnyAsync<IntegrationApiKey>(
      k => k.PartnerId == (Guid?) partnerId && k.Status != "Revoked",
      cancellationToken);
    if (hasKey)
      throw new InvalidOperationException("key_already_exists");

    if (!ApiKeyGenerator.IsValidEnv(env))
      throw new InvalidOperationException("invalid_env");

    // ═══════════════════════════════════════════════════════════════════
    // TWO independent cryptographic values:
    //   apiKeyRaw   → hashed with SHA-256 → stored as KeyHash (for DB lookup)
    //   hmacSecret  → encrypted with AES-GCM → stored as SecretProtected
    // ═══════════════════════════════════════════════════════════════════
    string apiKeyRaw = ApiKeyGenerator.GenerateSecret();
    string hmacSecret = ApiKeyGenerator.GenerateSecret();

    string keyHash = SecretHasher.Hash(apiKeyRaw);
    string prefix = ApiKeyGenerator.BuildPrefix(env);
    string secretProtected = this._protector.Protect(hmacSecret);

    var scopes1 = scopes?.ToList() ?? new List<string>();
    var rateLimits1 = rateLimits ?? (IDictionary<string, int>) new Dictionary<string, int>();

    var apiKey = new IntegrationApiKey(
      new Guid?(partnerId), keyHash, secretProtected, prefix, env,
      scopes1, rateLimits1, expiresAt, requireSignature);

    this._db.ApiKeys.Add(apiKey);
    await this._db.SaveChangesAsync(cancellationToken);
    await this.InvalidateCacheAsync(apiKey.Id, keyHash, cancellationToken);
    this._logger.LogInformation("Integration API key created for partner {PartnerId}", partnerId);

    return (apiKey, apiKeyRaw, hmacSecret);
  }

  /// <summary>
  /// Rotates both the API key and the HMAC secret for an existing key.
  /// Returns TWO new independent values.
  /// </summary>
  public async Task<(IntegrationApiKey ApiKey, string NewApiKeyRaw, string NewHmacSecret)> RotateKeyAsync(
    Guid keyId,
    CancellationToken cancellationToken)
  {
    var apiKey = await this._db.ApiKeys
      .FirstOrDefaultAsync<IntegrationApiKey>(k => k.Id == keyId, cancellationToken)
      ?? throw new InvalidOperationException("key_not_found");

    string oldHash = apiKey.KeyHash;

    // Generate TWO new independent values on rotation
    string newApiKeyRaw = ApiKeyGenerator.GenerateSecret();
    string newHmacSecret = ApiKeyGenerator.GenerateSecret();

    apiKey.Rotate(SecretHasher.Hash(newApiKeyRaw), this._protector.Protect(newHmacSecret));
    await this._db.SaveChangesAsync(cancellationToken);
    await this.InvalidateCacheAsync(apiKey.Id, oldHash, cancellationToken);

    return (apiKey, newApiKeyRaw, newHmacSecret);
  }

  public async Task RevokeKeyAsync(Guid keyId, CancellationToken cancellationToken)
  {
    var apiKey = await this._db.ApiKeys
      .FirstOrDefaultAsync<IntegrationApiKey>(k => k.Id == keyId, cancellationToken);
    if (apiKey == null)
      return;

    apiKey.Revoke();
    await this._db.SaveChangesAsync(cancellationToken);
    await this.InvalidateCacheAsync(apiKey.Id, apiKey.KeyHash, cancellationToken);
  }

  public Task<List<IntegrationApiKey>> GetApiKeysAsync(
    Guid? partnerId,
    CancellationToken cancellationToken)
  {
    IQueryable<IntegrationApiKey> source = this._db.ApiKeys.AsNoTracking<IntegrationApiKey>();
    if (partnerId.HasValue)
      source = source.Where<IntegrationApiKey>(k => k.PartnerId == (Guid?) partnerId.Value);
    return source
      .OrderByDescending<IntegrationApiKey, DateTime>(k => k.CreatedAt)
      .ToListAsync<IntegrationApiKey>(cancellationToken);
  }

  private async Task InvalidateCacheAsync(
    Guid keyId,
    string secretHash,
    CancellationToken cancellationToken)
  {
    IDatabase db = this._redis.GetDatabase();
    await db.KeyDeleteAsync((RedisKey) $"sys:integ:key:{keyId}");
    await db.KeyDeleteAsync((RedisKey) ("sys:integ:key:secret:" + secretHash));
  }

  public Task InvalidateKeyCacheAsync(IntegrationApiKey key, CancellationToken cancellationToken)
  {
    if (key == null)
      return Task.CompletedTask;
    return this.InvalidateCacheAsync(key.Id, key.KeyHash, cancellationToken);
  }
}
