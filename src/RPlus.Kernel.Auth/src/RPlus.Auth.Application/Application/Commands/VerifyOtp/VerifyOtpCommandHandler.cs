// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.VerifyOtp.VerifyOtpCommandHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using RPlus.SDK.Auth.Enums;
using RPlus.Auth.Domain.Entities;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Eventing.Abstractions;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, VerifyOtpResponse>
{
  private readonly IAuthDataService _authDataService;
  private readonly IOutboxRepository _outbox;
  private readonly IPhoneUtil _phoneUtil;
  private readonly ICryptoService _crypto;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<VerifyOtpCommandHandler> _logger;
  private readonly ISecurityMetrics _metrics;
  private readonly IUserAuthEventPublisher _eventPublisher;
  private readonly IEventPublisher _integrationEventPublisher;

  public VerifyOtpCommandHandler(
    IAuthDataService authDataService,
    IOutboxRepository outbox,
    IPhoneUtil phoneUtil,
    ICryptoService crypto,
    TimeProvider timeProvider,
    ILogger<VerifyOtpCommandHandler> logger,
    ISecurityMetrics metrics,
    IUserAuthEventPublisher eventPublisher,
    IEventPublisher integrationEventPublisher)
  {
    this._authDataService = authDataService;
    this._outbox = outbox;
    this._phoneUtil = phoneUtil;
    this._crypto = crypto;
    this._timeProvider = timeProvider;
    this._logger = logger;
    this._metrics = metrics;
    this._eventPublisher = eventPublisher;
    this._integrationEventPublisher = integrationEventPublisher;
  }

  public async Task<VerifyOtpResponse> Handle(
    VerifyOtpCommand request,
    CancellationToken cancellationToken)
  {
    string normalized = this._phoneUtil.NormalizeToE164(request.Phone);
    string phoneHash = this._crypto.HashPhone(normalized);
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    OtpChallengeEntity challenge = await this._authDataService.GetLatestOtpChallengeAsync(phoneHash, request.DeviceId, cancellationToken);
    if (challenge == null)
      return new VerifyOtpResponse(OtpVerifyStatus.NotFound, PhoneHash: phoneHash);
    if (challenge.ExpiresAt < now)
    {
      this._metrics.IncOtpVerification("expired");
      return new VerifyOtpResponse(OtpVerifyStatus.Expired, PhoneHash: phoneHash);
    }
    if (challenge.AttemptsLeft <= 0 || challenge.IsBlocked)
    {
      this._metrics.IncOtpVerification("attempts_exceeded");
      return new VerifyOtpResponse(OtpVerifyStatus.AttemptsExceeded, PhoneHash: phoneHash);
    }
    string otpHash = this._crypto.ComputeOtpHash(normalized, request.Code, challenge.CreatedAt);
    if (!CryptographicOperations.FixedTimeEquals((ReadOnlySpan<byte>) Convert.FromHexString(otpHash), (ReadOnlySpan<byte>) Convert.FromHexString(challenge.CodeHash)))
    {
      --challenge.AttemptsLeft;
      if (challenge.AttemptsLeft <= 0)
      {
        challenge.IsBlocked = true;
        challenge.BlockedAt = new DateTime?(now);
      }
      await this._authDataService.UpdateOtpChallengeAsync(challenge, cancellationToken);
      this._metrics.IncOtpVerification("invalid_code");
      this._logger.LogWarning("OTP verification failed (PhoneHash={PhoneHash}). Attempts left: {Attempts}", (object) phoneHash, (object) challenge.AttemptsLeft);
      return new VerifyOtpResponse(OtpVerifyStatus.InvalidCode, PhoneHash: phoneHash);
    }
    challenge.VerifiedAt = new DateTime?(now);
    await this._authDataService.UpdateOtpChallengeAsync(challenge, cancellationToken);
    AuthUserEntity user = await this._authDataService.GetUserByPhoneHashAsync(phoneHash, cancellationToken);
    if (user == null)
    {
      AuthUserEntity authUserEntity1 = new AuthUserEntity();
      authUserEntity1.Id = Guid.NewGuid();
      authUserEntity1.PhoneHash = phoneHash;
      AuthUserEntity authUserEntity2 = authUserEntity1;
      authUserEntity2.PhoneEncrypted = await this._crypto.EncryptPhoneAsync(normalized, cancellationToken);
      authUserEntity1.CreatedAt = now;
      authUserEntity1.LastLoginAt = new DateTime?(now);
      authUserEntity1.RegistrationIp = request.ClientIp;
      authUserEntity1.RegistrationUserAgent = request.UserAgent;
      authUserEntity1.RegistrationDeviceId = request.DeviceId;
      authUserEntity1.SecurityVersion = 1;
      authUserEntity1.PasswordVersion = 1;
      authUserEntity1.FailedAttempts = 0;
      user = authUserEntity1;
      authUserEntity2 = (AuthUserEntity) null;
      authUserEntity1 = (AuthUserEntity) null;
      AuthUserEntity userAsync = await this._authDataService.CreateUserAsync(user, cancellationToken);
      await this._eventPublisher.PublishUserCreatedAsync(user, (string) null, (string) null, (string) null, (System.Collections.Generic.Dictionary<string, string>?)null, cancellationToken);
      this._logger.LogInformation("AuthUserEntity created for user {UserId} on first login (published UserCreated)", (object) user.Id);
    }
    if (user.IsBlocked)
    {
      this._logger.LogWarning("User {UserId} is blocked (security reason: {Reason})", (object) user.Id, (object) user.BlockReason);
      this._metrics.IncOtpVerification("user_blocked");
      return new VerifyOtpResponse(OtpVerifyStatus.UserBlocked, PhoneHash: phoneHash);
    }
    user.LastLoginAt = new DateTime?(now);
    user.LastOtpSentAt = new DateTime?(now);
    user.FailedAttempts = 0;
    await this._authDataService.UpdateUserAsync(user, cancellationToken);
    DeviceEntity device = await this._authDataService.GetDeviceByUserAndKeyAsync(user.Id, request.DeviceId, cancellationToken);
    if (device == null)
    {
      device = new DeviceEntity()
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        DeviceKey = request.DeviceId,
        PublicJwk = request.DpopPublicJwk,
        CreatedAt = now,
        LastSeenAt = now
      };
      DeviceEntity updateDeviceAsync = await this._authDataService.CreateOrUpdateDeviceAsync(device, cancellationToken);
    }
    else
    {
      device.LastSeenAt = now;
      if (!string.IsNullOrEmpty(request.DpopPublicJwk))
        device.PublicJwk = request.DpopPublicJwk;
      DeviceEntity updateDeviceAsync = await this._authDataService.CreateOrUpdateDeviceAsync(device, cancellationToken);
    }
    IAuthDataService authDataService = this._authDataService;
    AuditLogEntity log = new AuditLogEntity();
    log.Id = Guid.NewGuid();
    log.UserId = new Guid?(user.Id);
    log.PhoneHash = phoneHash;
    log.Action = "otp_verify";
    log.Result = "success";
    log.CreatedAt = now;
    log.Ip = request.ClientIp;
    log.UserAgent = request.UserAgent;
    log.DeviceId = request.DeviceId;
    log.MetadataJson = JsonSerializer.Serialize(new
    {
      challengeId = challenge.Id
    });
    CancellationToken ct = cancellationToken;
    await authDataService.CreateAuditLogAsync(log, ct);
    await this._eventPublisher.PublishUserAuthUpdatedAsync(user.Id, request.ClientIp, cancellationToken);
    try
    {
      string aggregateId = user.Id.ToString();
      string ipAddress = string.IsNullOrWhiteSpace(request.ClientIp) ? "unknown" : request.ClientIp;
      DateTime loginTimestamp = this._timeProvider.GetUtcNow().UtcDateTime;
      await this._integrationEventPublisher.PublishAsync<UserLoggedIn>(
        new UserLoggedIn(aggregateId, request.DeviceId, "Otp", ipAddress, request.UserAgent, loginTimestamp),
        "auth.user.logged_in.v1",
        aggregateId,
        cancellationToken);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish UserLoggedIn event for OTP login");
    }
    this._metrics.IncOtpVerification("success");
    this._metrics.IncTokenIssued();
    return new VerifyOtpResponse(OtpVerifyStatus.Success, user, device, phoneHash);
  }
}
