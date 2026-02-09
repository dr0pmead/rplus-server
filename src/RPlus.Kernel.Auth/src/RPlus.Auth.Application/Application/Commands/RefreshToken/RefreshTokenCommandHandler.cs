// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.RefreshToken.RefreshTokenCommandHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Options;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
  private readonly IAuthDataService _authDataService;
  private readonly ICryptoService _crypto;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<RefreshTokenCommandHandler> _logger;
  private readonly ISecurityMetrics _metrics;
  private readonly IOptions<RiskManagementOptions> _riskOptions;

  public RefreshTokenCommandHandler(
    IAuthDataService authDataService,
    ICryptoService crypto,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCommandHandler> logger,
    ISecurityMetrics metrics,
    IOptions<RiskManagementOptions> riskOptions)
  {
    this._authDataService = authDataService;
    this._crypto = crypto;
    this._timeProvider = timeProvider;
    this._logger = logger;
    this._metrics = metrics;
    this._riskOptions = riskOptions;
  }

  public async Task<RefreshTokenResponse> Handle(
    RefreshTokenCommand request,
    CancellationToken cancellationToken)
  {
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    if (string.IsNullOrWhiteSpace(request.RefreshToken) || !request.RefreshToken.Contains('.'))
    {
      this._logger.LogWarning("Malformed refresh token received.");
      return new RefreshTokenResponse(false, ErrorCode: "invalid_token");
    }
    string[] strArray = request.RefreshToken.Split('.', 2);
    if (strArray.Length != 2)
    {
      this._logger.LogWarning("Invalid refresh token format.");
      return new RefreshTokenResponse(false, ErrorCode: "invalid_token");
    }
    string tokenHash = this._crypto.HashRefreshSecret(strArray[1]);
    RefreshTokenEntity tokenEntity = await this._authDataService.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
    if (tokenEntity == null)
    {
      this._logger.LogWarning("Refresh token not found for hash: {TokenHash}", (object) tokenHash);
      return new RefreshTokenResponse(false, ErrorCode: "invalid_token");
    }
    if (tokenEntity.IsRevoked || tokenEntity.UsedAt.HasValue)
    {
      this._logger.LogCritical("THEFT DETECTED: Refresh token already used or revoked! Family={Family}, Token={TokenHash}", (object) tokenEntity.TokenFamily, (object) tokenHash);
      await this._authDataService.RevokeRefreshTokenFamilyAsync(tokenEntity.TokenFamily, "theft_detected", cancellationToken);
      IAuthDataService authDataService = this._authDataService;
      AuditLogEntity log = new AuditLogEntity();
      log.Id = Guid.NewGuid();
      log.UserId = new Guid?(tokenEntity.UserId);
      log.Action = "token_theft_detected";
      log.Result = "failure";
      log.CreatedAt = now;
      log.Ip = request.ClientIp;
      log.UserAgent = request.UserAgent;
      log.DeviceId = request.DeviceId;
      log.IsSuspicious = true;
      log.RiskLevel = "critical";
      log.MetadataJson = JsonSerializer.Serialize(new
      {
        family = tokenEntity.TokenFamily,
        tokenId = tokenEntity.Id
      });
      CancellationToken ct = cancellationToken;
      await authDataService.CreateAuditLogAsync(log, ct);
      this._metrics.IncTokenRefresh("theft_detected");
      this._metrics.IncRiskDetected("critical");
      return new RefreshTokenResponse(false, ErrorCode: "token_compromised");
    }
    if (tokenEntity.ExpiresAt < now)
    {
      this._metrics.IncTokenRefresh("expired");
      return new RefreshTokenResponse(false, ErrorCode: "token_expired");
    }
    AuthUserEntity user = await this._authDataService.GetUserByIdAsync(tokenEntity.UserId, cancellationToken);
    if (user == null || user.IsBlocked)
      return new RefreshTokenResponse(false, ErrorCode: "user_blocked");
    DeviceEntity device = await this._authDataService.GetDeviceByUserAndKeyAsync(user.Id, request.DeviceId, cancellationToken);
    if (device == null || device.IsBlocked)
      return new RefreshTokenResponse(false, ErrorCode: "device_unauthorized");
    if (!string.IsNullOrEmpty(tokenEntity.DeviceFingerprint) && tokenEntity.DeviceFingerprint != request.UserAgent)
      this._logger.LogWarning("Device fingerprint mismatch during refresh. Expected: {Expected}, Actual: {Actual}", (object) tokenEntity.DeviceFingerprint, (object) request.UserAgent);
    AuthSessionEntity session = await this._authDataService.GetSessionByIdAsync(tokenEntity.SessionId, cancellationToken);
    if (session == null || session.RevokedAt.HasValue || session.ExpiresAt < now)
      return new RefreshTokenResponse(false, ErrorCode: "session_invalid");
    int num = 0;
    List<string> values = new List<string>();
    RiskManagementOptions managementOptions = this._riskOptions.Value;
    if (!string.IsNullOrEmpty(request.ClientIp) && session.IssuerIp != request.ClientIp)
    {
      num += managementOptions.IpChangeScore;
      values.Add($"IP changed from {session.IssuerIp} to {request.ClientIp}");
    }
    if (!string.IsNullOrEmpty(request.UserAgent) && session.IssuerUserAgent != request.UserAgent)
    {
      num += managementOptions.UserAgentChangeScore;
      values.Add("User-Agent changed");
    }
    if (num > 0)
    {
      session.RiskScore += num;
      session.IsSuspicious = session.RiskScore >= managementOptions.SuspiciousThreshold;
      AuthSessionEntity authSessionEntity = session;
      int riskScore = session.RiskScore;
      string str1 = riskScore < managementOptions.CriticalThreshold ? (riskScore < managementOptions.SuspiciousThreshold ? (riskScore < 25 ? "low" : "medium") : "high") : "critical";
      authSessionEntity.RiskLevel = str1;
      string str2 = string.Join("; ", (IEnumerable<string>) values);
      session.SuspiciousActivityDetails = string.IsNullOrEmpty(session.SuspiciousActivityDetails) ? str2 : $"{session.SuspiciousActivityDetails} | {str2}";
      this._logger.LogWarning("Risk increased for session {SessionId}. Score: {Score}, Details: {Details}", (object) session.Id, (object) session.RiskScore, (object) str2);
      if (session.RiskScore >= managementOptions.CriticalThreshold)
      {
        this._logger.LogCritical("CRITICAL RISK: Revoking session {SessionId} due to high risk score.", (object) session.Id);
        session.RevokedAt = new DateTime?(now);
        session.RevokeReason = "high_risk_detected";
        await this._authDataService.UpdateSessionAsync(session, cancellationToken);
        await this._authDataService.RevokeRefreshTokenFamilyAsync(tokenEntity.TokenFamily, "high_risk_detected", cancellationToken);
        this._metrics.IncSessionRevoked("high_risk");
        this._metrics.IncRiskDetected("critical");
        return new RefreshTokenResponse(false, ErrorCode: "session_revoked_security");
      }
    }
    tokenEntity.UsedAt = new DateTime?(now);
    await this._authDataService.UpdateRefreshTokenAsync(tokenEntity, cancellationToken);
    session.LastActivityAt = new DateTime?(now);
    session.IssuerIp = request.ClientIp ?? session.IssuerIp;
    session.IssuerUserAgent = request.UserAgent ?? session.IssuerUserAgent;
    await this._authDataService.UpdateSessionAsync(session, cancellationToken);
    IAuthDataService authDataService1 = this._authDataService;
    AuditLogEntity log1 = new AuditLogEntity();
    log1.Id = Guid.NewGuid();
    log1.UserId = new Guid?(user.Id);
    log1.Action = "token_refresh";
    log1.Result = "success";
    log1.CreatedAt = now;
    log1.Ip = request.ClientIp;
    log1.UserAgent = request.UserAgent;
    log1.IsSuspicious = session.IsSuspicious;
    log1.RiskLevel = session.RiskLevel;
    log1.DeviceId = request.DeviceId;
    log1.MetadataJson = JsonSerializer.Serialize(new
    {
      sessionId = session.Id,
      tokenId = tokenEntity.Id,
      riskScore = session.RiskScore
    });
    CancellationToken ct1 = cancellationToken;
    await authDataService1.CreateAuditLogAsync(log1, ct1);
    this._metrics.IncTokenRefresh("success");
    this._metrics.IncTokenIssued();
    return new RefreshTokenResponse(true, user, device, session);
  }
}
