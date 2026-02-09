// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Infrastructure.Services.PowService
// Assembly: RPlus.Kernel.Guard.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DF97D949-B080-4EE7-A993-4CFFBB255DD1
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Infrastructure.dll

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Guard.Application.Services;
using StackExchange.Redis;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Guard.Infrastructure.Services;

public sealed class PowService : IPowService
{
  private const string ChallengePrefix = "sys:guard:pow:challenge:";
  private readonly IConnectionMultiplexer _redis;
  private readonly ILogger<PowService> _logger;
  private readonly int _difficulty;
  private readonly TimeSpan _ttl;
  private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  public PowService(
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<PowService> logger)
  {
    this._redis = redis;
    this._logger = logger;
    this._difficulty = configuration.GetValue<int?>("Guard:Pow:Difficulty") ?? 4;
    this._ttl = TimeSpan.FromSeconds((long) Math.Clamp(configuration.GetValue<int?>("Guard:Pow:TtlSeconds") ?? 120, 15, 900));
  }

  public async Task<PowChallenge> CreateChallengeAsync(string? scope, CancellationToken ct)
  {
    string challengeId = Guid.NewGuid().ToString("N");
    string salt = PowService.GenerateSalt(16 /*0x10*/);
    DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(this._ttl);
    
    var powChallengeState = new PowChallengeState
    {
        Salt = salt,
        Difficulty = this._difficulty,
        Scope = scope,
        ExpiresAtUtc = expiresAt
    };

    IDatabase database = this._redis.GetDatabase();
    string str = JsonSerializer.Serialize(powChallengeState, this._jsonOptions);
    bool set = await database.StringSetAsync($"sys:guard:pow:challenge:{challengeId}", str, this._ttl);
    
    return new PowChallenge(challengeId, salt, this._difficulty, expiresAt, scope);
  }

  public async Task<PowVerifyResult> VerifyAsync(
    string challengeId,
    string nonce,
    CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(nonce))
      return new PowVerifyResult(false, "invalid_request", (string) null);
    IDatabase db = this._redis.GetDatabase();
    RedisValue async = await db.StringGetAsync((RedisKey) ("sys:guard:pow:challenge:" + challengeId));
    if (async.IsNullOrEmpty)
      return new PowVerifyResult(false, "challenge_not_found", (string) null);
    PowService.PowChallengeState powChallengeState;
    try
    {
      powChallengeState = JsonSerializer.Deserialize<PowService.PowChallengeState>(async.ToString(), this._jsonOptions);
    }
    catch (JsonException ex)
    {
      this._logger.LogWarning((Exception) ex, "Invalid PoW challenge payload.");
      return new PowVerifyResult(false, "challenge_invalid", (string) null);
    }
    if (powChallengeState == null)
      return new PowVerifyResult(false, "challenge_invalid", (string) null);
    string hash = PowService.ComputeHashHex($"{challengeId}:{nonce}:{powChallengeState.Salt}");
    if (!hash.StartsWith(new string('0', Math.Max(1, powChallengeState.Difficulty)), StringComparison.Ordinal))
      return new PowVerifyResult(false, "pow_failed", hash);
    int num = await db.KeyDeleteAsync((RedisKey) ("sys:guard:pow:challenge:" + challengeId)) ? 1 : 0;
    return new PowVerifyResult(true, (string) null, hash);
  }

  private static string GenerateSalt(int lengthBytes)
  {
    return Convert.ToHexString(RandomNumberGenerator.GetBytes(lengthBytes)).ToLowerInvariant();
  }

  private static string ComputeHashHex(string value)
  {
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
  }

  private sealed class PowChallengeState
  {
    public string Salt { get; init; } = string.Empty;

    public int Difficulty { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string? Scope { get; init; }
  }
}
