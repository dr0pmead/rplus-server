// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Services.OtpService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.SDK.Auth.Enums;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Options;
using RPlus.Auth.Domain.Entities;
using System;
using System.Globalization;
using StackExchange.Redis;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class OtpService : IOtpService
{
  private readonly IAuthDataService _authDataService;
  private readonly IOutboxRepository _outbox;
  private readonly IPhoneUtil _phoneUtil;
  private readonly ICryptoService _crypto;
  private readonly IOptionsMonitor<OtpOptions> _otpOptions;
  private readonly IOptions<OtpRateLimitOptions> _otpRateLimitOptions;
  private readonly IHostEnvironment _environment;
  private readonly TimeProvider _timeProvider;
  private readonly IOtpDeliveryService _smsSender;
  private readonly ILogger<OtpService> _logger;
  private readonly IRedisRateLimitService _rateLimitService;
  private readonly IConnectionMultiplexer? _redis;

  public OtpService(
    IAuthDataService authDataService,
    IOutboxRepository outbox,
    IPhoneUtil phoneUtil,
    ICryptoService crypto,
    IOptionsMonitor<OtpOptions> otpOptions,
    IOptions<OtpRateLimitOptions> otpRateLimitOptions,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    IOtpDeliveryService smsSender,
    ILogger<OtpService> logger,
    IRedisRateLimitService rateLimitService,
    IConnectionMultiplexer? redis = null)
  {
    this._authDataService = authDataService;
    this._outbox = outbox;
    this._phoneUtil = phoneUtil;
    this._crypto = crypto;
    this._otpOptions = otpOptions;
    this._otpRateLimitOptions = otpRateLimitOptions;
    this._environment = environment;
    this._timeProvider = timeProvider;
    this._smsSender = smsSender;
    this._logger = logger;
    this._rateLimitService = rateLimitService;
    this._redis = redis;
  }

  public async Task<OtpRequestResult> RequestOtpAsync(
    string phone,
    string deviceId,
    string? clientIp,
    string? userAgent,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(deviceId))
      return new OtpRequestResult(false, 0, (string) null, "device_id_required");
    string normalized = this._phoneUtil.NormalizeToE164(phone);
    string phoneHash = this._crypto.HashPhone(normalized);
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    (int limit, int windowSeconds) = this._otpRateLimitOptions.Value.GetPerMinuteLimit();
    (bool flag, int RetryAfterSeconds) = await this._rateLimitService.CheckRateLimitAsync("otp:min:" + phoneHash, limit, windowSeconds, cancellationToken);
    if (!flag)
      return new OtpRequestResult(false, RetryAfterSeconds, (string) null, "rate_limit_exceeded");
    AuthUserEntity user = await this._authDataService.GetUserByPhoneHashAsync(phoneHash, cancellationToken);
    if (user != null && user.IsBlocked)
    {
      this._logger.LogWarning("OTP request rejected for blocked user. PhoneHash={PhoneHash}", (object) phoneHash);
      return new OtpRequestResult(false, 0, (string) null, "user_blocked", true);
    }
    OtpChallengeEntity otpChallengeAsync1 = await this._authDataService.GetLatestOtpChallengeAsync(phoneHash, deviceId, cancellationToken);
    if (otpChallengeAsync1 != null && otpChallengeAsync1.CreatedAt.AddSeconds((double) windowSeconds) > now)
      return new OtpRequestResult(false, (int) Math.Ceiling((otpChallengeAsync1.CreatedAt.AddSeconds((double) windowSeconds) - now).TotalSeconds), (string) null, "too_soon", user != null);
    OtpOptions currentValue = this._otpOptions.CurrentValue;
    string code = OtpService.GenerateCode(currentValue.Digits);
    DateTime dateTime = now.AddSeconds((double) currentValue.TtlSeconds);
    OtpChallengeEntity challenge = new OtpChallengeEntity()
    {
      Id = Guid.NewGuid(),
      PhoneHash = phoneHash,
      UserId = user?.Id,
      CodeHash = this._crypto.ComputeOtpHash(normalized, code, now),
      ChallengeType = user == null ? "registration" : "login",
      CreatedAt = now,
      ExpiresAt = dateTime,
      AttemptsLeft = currentValue.MaxAttempts,
      IssuerIp = clientIp ?? "unknown",
      IssuerDeviceId = deviceId,
      DeliveryChannel = "sms",
      DeliveryStatus = "pending"
    };
    string str = "Код подтверждения: " + code;
    challenge.DeliveryStatus = "sent";
    challenge.DeliveredAt = new DateTime?(now);
    OtpChallengeEntity otpChallengeAsync2 = await this._authDataService.CreateOtpChallengeAsync(challenge, cancellationToken);
    IAuthDataService authDataService = this._authDataService;
    AuditLogEntity log = new AuditLogEntity();
    log.Id = Guid.NewGuid();
    log.UserId = user?.Id;
    log.PhoneHash = phoneHash;
    log.Action = "otp_request";
    log.Result = "success";
    log.CreatedAt = now;
    log.Ip = clientIp;
    log.UserAgent = userAgent;
    log.DeviceId = deviceId;
    log.MetadataJson = JsonSerializer.Serialize(new
    {
      challengeId = challenge.Id,
      type = challenge.ChallengeType
    });
    CancellationToken ct = cancellationToken;
    await authDataService.CreateAuditLogAsync(log, ct);
    return new OtpRequestResult(true, 0, code, AccountExists: user != null);
  }

  public async Task<OtpVerifyResult> VerifyOtpAsync(
    string phone,
    string code,
    string deviceId,
    string? dpopPublicJwk,
    string? clientIp,
    string? userAgent,
    CancellationToken cancellationToken)
  {
    string normalized = this._phoneUtil.NormalizeToE164(phone);
    string phoneHash = this._crypto.HashPhone(normalized);
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    OtpChallengeEntity challenge = await this._authDataService.GetLatestOtpChallengeAsync(phoneHash, deviceId, cancellationToken);
    if (challenge == null)
      return new OtpVerifyResult(OtpVerifyStatus.NotFound, (AuthUserEntity) null, (DeviceEntity) null, phoneHash);
    if (challenge.ExpiresAt < now)
      return new OtpVerifyResult(OtpVerifyStatus.Expired, (AuthUserEntity) null, (DeviceEntity) null, phoneHash);
    if (challenge.AttemptsLeft <= 0 || challenge.IsBlocked)
      return new OtpVerifyResult(OtpVerifyStatus.AttemptsExceeded, (AuthUserEntity) null, (DeviceEntity) null, phoneHash);
    if (!CryptographicOperations.FixedTimeEquals((ReadOnlySpan<byte>) Convert.FromHexString(this._crypto.ComputeOtpHash(normalized, code, challenge.CreatedAt)), (ReadOnlySpan<byte>) Convert.FromHexString(challenge.CodeHash)))
    {
      --challenge.AttemptsLeft;
      if (challenge.AttemptsLeft <= 0)
      {
        challenge.IsBlocked = true;
        challenge.BlockedAt = new DateTime?(now);
      }
      await this._authDataService.UpdateOtpChallengeAsync(challenge, cancellationToken);
      return new OtpVerifyResult(OtpVerifyStatus.InvalidCode, (AuthUserEntity) null, (DeviceEntity) null, phoneHash);
    }
    challenge.VerifiedAt = new DateTime?(now);
    await this._authDataService.UpdateOtpChallengeAsync(challenge, cancellationToken);
    AuthUserEntity user = await this._authDataService.GetUserByPhoneHashAsync(phoneHash, cancellationToken);
    bool isNewUser = false;
    if (user == null)
    {
      AuthUserEntity authUserEntity1 = new AuthUserEntity();
      authUserEntity1.Id = Guid.NewGuid();
      authUserEntity1.PhoneHash = phoneHash;
      AuthUserEntity authUserEntity2 = authUserEntity1;
      authUserEntity2.PhoneEncrypted = await this._crypto.EncryptPhoneAsync(normalized, cancellationToken);
      authUserEntity1.CreatedAt = now;
      authUserEntity1.LastLoginAt = new DateTime?(now);
      authUserEntity1.RegistrationIp = clientIp;
      authUserEntity1.RegistrationUserAgent = userAgent;
      authUserEntity1.RegistrationDeviceId = deviceId;
      authUserEntity1.SecurityVersion = 1;
      authUserEntity1.PasswordVersion = 1;
      user = authUserEntity1;
      authUserEntity2 = (AuthUserEntity) null;
      authUserEntity1 = (AuthUserEntity) null;
      AuthUserEntity userAsync = await this._authDataService.CreateUserAsync(user, cancellationToken);
      isNewUser = true;
      IOutboxRepository outbox1 = this._outbox;
      OutboxMessageEntity message1 = new OutboxMessageEntity();
      message1.Id = Guid.NewGuid();
      message1.Topic = "auth.users";
      message1.EventType = "UserRegistered";
      message1.Payload = JsonSerializer.Serialize(new
      {
        UserId = user.Id,
        Phone = normalized,
        RegisteredAt = now,
        Ip = clientIp,
        UserAgent = userAgent
      });
      message1.AggregateId = user.Id.ToString();
      message1.CreatedAt = now;
      CancellationToken cancellationToken1 = cancellationToken;
      await outbox1.AddAsync(message1, cancellationToken1);
      IOutboxRepository outbox2 = this._outbox;
      OutboxMessageEntity message2 = new OutboxMessageEntity();
      message2.Id = Guid.NewGuid();
      message2.Topic = "auth.loyalty";
      message2.EventType = "AwardRegistrationBonus";
      message2.Payload = JsonSerializer.Serialize(new
      {
        UserId = user.Id,
        Points = 100,
        Reason = "Registration Bonus"
      });
      message2.AggregateId = user.Id.ToString();
      message2.CreatedAt = now;
      CancellationToken cancellationToken2 = cancellationToken;
      await outbox2.AddAsync(message2, cancellationToken2);
    }
    else
    {
      if (user.IsBlocked)
        return new OtpVerifyResult(OtpVerifyStatus.UserBlocked, (AuthUserEntity) null, (DeviceEntity) null, phoneHash);
      user.LastLoginAt = new DateTime?(now);
      await this._authDataService.UpdateUserAsync(user, cancellationToken);
    }
    DeviceEntity device = await this._authDataService.GetDeviceByUserAndKeyAsync(user.Id, deviceId, cancellationToken);
    if (device == null)
    {
      device = new DeviceEntity()
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        DeviceKey = deviceId,
        PublicJwk = dpopPublicJwk,
        CreatedAt = now,
        LastSeenAt = now
      };
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
    log.Ip = clientIp;
    log.UserAgent = userAgent;
    log.DeviceId = deviceId;
    log.MetadataJson = JsonSerializer.Serialize(new
    {
      isNewUser = isNewUser,
      challengeId = challenge.Id
    });
    CancellationToken ct = cancellationToken;
    await authDataService.CreateAuditLogAsync(log, ct);
    return new OtpVerifyResult(OtpVerifyStatus.Success, user, device, phoneHash);
  }

  private static string GenerateCode(int digits)
  {
    digits = Math.Clamp(digits, 4, 10);
    int int32 = RandomNumberGenerator.GetInt32(0, (int) Math.Pow(10.0, (double) digits));
    // ISSUE: explicit reference operation
    return int32.ToString($"D{digits}");
  }
}
