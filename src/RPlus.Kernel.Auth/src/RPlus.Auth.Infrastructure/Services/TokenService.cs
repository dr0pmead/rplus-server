// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.TokenService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Options;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
  private readonly IAuthDataService _authDataService;
  private readonly ICryptoService _crypto;
  private readonly IOptionsMonitor<JwtOptions> _jwtOptions;
  private readonly IJwtKeyProvider _keyProvider;
  private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<TokenService> _logger;

  public TokenService(
    IAuthDataService authDataService,
    ICryptoService crypto,
    IOptionsMonitor<JwtOptions> jwtOptions,
    IJwtKeyProvider keyProvider,
    TimeProvider timeProvider,
    ILogger<TokenService> logger)
  {
    this._authDataService = authDataService;
    this._crypto = crypto;
    this._jwtOptions = jwtOptions;
    this._keyProvider = keyProvider;
    this._timeProvider = timeProvider;
    this._logger = logger;
  }

  public async Task<TokenPair> IssueTokensAsync(
    AuthUserEntity user,
    DeviceEntity device,
    string? dpopThumbprint,
    string? ip,
    string? userAgent,
    CancellationToken cancellationToken)
  {
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    AuthSessionEntity session = new AuthSessionEntity()
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      DeviceId = device.DeviceKey,
      DeviceFingerprint = this.ComputeFingerprint(userAgent, ip, device.DeviceKey),
      IssuedAt = now,
      ExpiresAt = now.AddDays(30.0),
      IssuerIp = ip ?? "unknown",
      IssuerUserAgent = userAgent ?? "unknown",
      RiskLevel = "low"
    };
    await this._authDataService.CreateSessionAsync(session, cancellationToken);
    return await this.CreateTokenPairAsync(user, session, device, now, ip, userAgent, null, dpopThumbprint, cancellationToken);
  }

  public async Task<TokenOperationResult> RefreshAsync(
    string refreshToken,
    string deviceIdentifier,
    string? dpopThumbprint,
    string? ip,
    string? userAgent,
    CancellationToken cancellationToken)
  {
    if (!TokenService.TryParseRefreshToken(refreshToken, out Guid _, out string secret))
      return new TokenOperationResult(TokenOperationStatus.InvalidSignature, null);
    RefreshTokenEntity? existing = await this._authDataService.GetRefreshTokenByHashAsync(this._crypto.HashRefreshSecret(secret), cancellationToken);
    if (existing == null)
      return new TokenOperationResult(TokenOperationStatus.NotFound, null);
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    if (existing.UsedAt.HasValue || existing.IsRevoked)
    {
      this._logger.LogCritical("TOKEN THEFT DETECTED! Refresh token reused. TokenFamily={Family}, UserId={User}", (object) existing.TokenFamily, (object) existing.UserId);
      await this._authDataService.RevokeRefreshTokenFamilyAsync(existing.TokenFamily, "token_theft_detected", cancellationToken);
      return new TokenOperationResult(TokenOperationStatus.TheftDetected, null);
    }
    if (existing.ExpiresAt < now)
      return new TokenOperationResult(TokenOperationStatus.Revoked, null);
    DeviceEntity? existingDevice = existing.Device;
    if (existingDevice == null || !string.Equals(existingDevice.DeviceKey, deviceIdentifier, StringComparison.Ordinal))
      return new TokenOperationResult(TokenOperationStatus.DeviceMismatch, null);
    if (!string.IsNullOrEmpty(existing.DpopThumbprint) && existing.DpopThumbprint != dpopThumbprint)
    {
      string expected = existing.DpopThumbprint ?? "unknown";
      string actual = dpopThumbprint ?? "none";
      this._logger.LogWarning("DPoP Mismatch on refresh. Expected: {Exp}, Actual: {Act}", expected, actual);
    }
    AuthUserEntity? user = await this._authDataService.GetUserByIdAsync(existing.UserId, cancellationToken);
    if (user == null || user.IsBlocked)
      return new TokenOperationResult(TokenOperationStatus.UserBlocked, null);
    existing.UsedAt = new DateTime?(now);
    await this._authDataService.UpdateRefreshTokenAsync(existing, cancellationToken);
    AuthSessionEntity? sessionByIdAsync = await this._authDataService.GetSessionByIdAsync(existing.SessionId, cancellationToken);
    if (sessionByIdAsync == null || sessionByIdAsync.RevokedAt.HasValue)
      return new TokenOperationResult(TokenOperationStatus.Revoked, null);
    string? dpopThumbprint1 = dpopThumbprint ?? existing.DpopThumbprint;
    TokenPair tokens = await this.CreateTokenPairAsync(user, sessionByIdAsync, existingDevice, now, ip, userAgent, existing, dpopThumbprint1, cancellationToken);
    return new TokenOperationResult(TokenOperationStatus.Success, tokens);
  }

  public async Task RevokeAsync(
    string refreshToken,
    string deviceIdentifier,
    CancellationToken cancellationToken)
  {
    if (!TokenService.TryParseRefreshToken(refreshToken, out Guid _, out string secret))
      return;
    RefreshTokenEntity? existing = await this._authDataService.GetRefreshTokenByHashAsync(this._crypto.HashRefreshSecret(secret), cancellationToken);
    if (existing == null)
      return;
    if (!string.Equals(existing.Device?.DeviceKey, deviceIdentifier, StringComparison.Ordinal))
      return;
    await this._authDataService.RevokeRefreshTokenFamilyAsync(existing.TokenFamily, "user_logout", cancellationToken);
    await this._authDataService.RevokeSessionAsync(existing.SessionId, "user_logout", cancellationToken);
  }

  private async Task<TokenPair> CreateTokenPairAsync(
    AuthUserEntity user,
    AuthSessionEntity session,
    DeviceEntity device,
    DateTime now,
    string? ip,
    string? userAgent,
    RefreshTokenEntity? previous,
    string? dpopThumbprint,
    CancellationToken cancellationToken)
  {
    JwtOptions currentValue = this._jwtOptions.CurrentValue;
    SigningCredentials signingCredentials = this._keyProvider.GetSigningCredentials();
    DateTime accessExpires = now.AddMinutes((double) currentValue.AccessTokenMinutes);
    DateTime refreshExpires = now.AddMinutes((double) currentValue.RefreshTokenMinutes);
    Guid guid1 = Guid.NewGuid();
    string str1 = guid1.ToString();
    List<Claim> claimList = new List<Claim>();
    guid1 = user.Id;
    claimList.Add(new Claim("sub", guid1.ToString()));
    guid1 = session.Id;
    claimList.Add(new Claim("sid", guid1.ToString()));
    guid1 = user.TenantId;
    claimList.Add(new Claim("tenant_id", guid1.ToString()));
    claimList.Add(new Claim("device_id", device.DeviceKey));
    claimList.Add(new Claim("jti", str1));
    claimList.Add(new Claim("iat", EpochTime.GetIntDate(now).ToString(), "http://www.w3.org/2001/XMLSchema#integer64"));
    claimList.Add(new Claim("ver", user.SecurityVersion.ToString()));
    List<Claim> source = claimList;
    if (!string.IsNullOrEmpty(dpopThumbprint))
      source.Add(new Claim("cnf", JsonSerializer.Serialize(new
      {
        jkt = dpopThumbprint
      }), "JSON"));
    JwtSecurityToken token = new JwtSecurityToken(currentValue.Issuer, currentValue.Audience, (IEnumerable<Claim>) source, new DateTime?(now), new DateTime?(accessExpires), signingCredentials);
    token.Header["kid"] = (object) this._keyProvider.GetKeyId();
    if (!string.IsNullOrWhiteSpace(device.PublicJwk))
    {
      if (string.IsNullOrEmpty(dpopThumbprint))
      {
        try
        {
          Dictionary<string, object>? dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(device.PublicJwk);
          if (dictionary != null)
          {
            if (!source.Any<Claim>((Func<Claim, bool>) (c => c.Type == "cnf")))
              token.Payload["cnf"] = (object) new Dictionary<string, object>()
              {
                ["jwk"] = (object) dictionary
              };
          }
        }
        catch
        {
        }
      }
    }
    string accessToken = this._tokenHandler.WriteToken((SecurityToken) token);
    Guid guid2 = Guid.NewGuid();
    string secureToken = this._crypto.GenerateSecureToken();
    string tokenValue = $"{guid2:N}.{secureToken}";
    string str2 = this._crypto.HashRefreshSecret(secureToken);
    RefreshTokenEntity refreshTokenEntity = new RefreshTokenEntity();
    refreshTokenEntity.Id = guid2;
    refreshTokenEntity.SessionId = session.Id;
    refreshTokenEntity.UserId = user.Id;
    refreshTokenEntity.DeviceId = device.Id;
    refreshTokenEntity.TokenHash = str2;
    string? tokenFamily = previous?.TokenFamily;
    if (tokenFamily == null)
    {
      guid1 = Guid.NewGuid();
      tokenFamily = guid1.ToString("N");
    }
    refreshTokenEntity.TokenFamily = tokenFamily;
    refreshTokenEntity.IssuedAt = now;
    refreshTokenEntity.ExpiresAt = refreshExpires;
    refreshTokenEntity.DeviceFingerprint = this.ComputeFingerprint(userAgent, ip, device.DeviceKey);
    refreshTokenEntity.ReplacedById = null;
    refreshTokenEntity.DpopThumbprint = dpopThumbprint;
    RefreshTokenEntity refresh = refreshTokenEntity;
    await this._authDataService.CreateRefreshTokenAsync(refresh, cancellationToken);
    if (previous != null)
    {
      previous.ReplacedById = new Guid?(refresh.Id);
      await this._authDataService.UpdateRefreshTokenAsync(previous, cancellationToken);
    }
    return new TokenPair(accessToken, accessExpires, tokenValue, refreshExpires);
  }

  private string ComputeFingerprint(string? ua, string? ip, string deviceId)
  {
    string s = $"{ua}|{ip}|{deviceId}";
    using (SHA256 shA256 = SHA256.Create())
      return Convert.ToHexString(shA256.ComputeHash(Encoding.UTF8.GetBytes(s)));
  }

  private static bool TryParseRefreshToken(string token, out Guid tokenId, out string secret)
  {
    tokenId = Guid.Empty;
    secret = string.Empty;
    if (string.IsNullOrWhiteSpace(token))
      return false;
    string[] strArray = token.Split('.', 2);
    if (strArray.Length != 2 || !Guid.TryParse(strArray[0], out tokenId))
      return false;
    secret = strArray[1];
    return true;
  }
}
