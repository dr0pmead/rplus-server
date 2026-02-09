// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.RequestOtp.RequestOtpCommandHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Options;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.RequestOtp;

public class RequestOtpCommandHandler : IRequestHandler<RequestOtpCommand, RequestOtpResponse>
{
  private readonly IAuthDataService _authDataService;
  private readonly IPhoneUtil _phoneUtil;
  private readonly ICryptoService _crypto;
  private readonly IOptionsMonitor<OtpOptions> _otpOptions;
  private readonly IOptions<OtpRateLimitOptions> _otpRateLimitOptions;
  private readonly IHostEnvironment _environment;
  private readonly IRedisRateLimitService _rateLimitService;
  private readonly IOtpDeliveryService _otpDeliveryService;
  private readonly IProtectionService _protectionService;
  private readonly ISecurityMetrics _metrics;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<RequestOtpCommandHandler> _logger;

  public RequestOtpCommandHandler(
    IAuthDataService authDataService,
    IPhoneUtil phoneUtil,
    ICryptoService crypto,
    IOptionsMonitor<OtpOptions> otpOptions,
    IOptions<OtpRateLimitOptions> otpRateLimitOptions,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<RequestOtpCommandHandler> logger,
    IRedisRateLimitService rateLimitService,
    IOtpDeliveryService otpDeliveryService,
    IProtectionService protectionService,
    ISecurityMetrics metrics)
  {
    this._authDataService = authDataService;
    this._phoneUtil = phoneUtil;
    this._crypto = crypto;
    this._otpOptions = otpOptions;
    this._otpRateLimitOptions = otpRateLimitOptions;
    this._environment = environment;
    this._timeProvider = timeProvider;
    this._logger = logger;
    this._rateLimitService = rateLimitService;
    this._otpDeliveryService = otpDeliveryService;
    this._protectionService = protectionService;
    this._metrics = metrics;
  }

  public async Task<RequestOtpResponse> Handle(
    RequestOtpCommand request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Nonce))
      return new RequestOtpResponse(false, ErrorCode: "pow_missing");
    if (!await this._protectionService.VerifySolutionAsync(request.ChallengeId, request.Nonce, request.ClientIp, cancellationToken))
      return new RequestOtpResponse(false, ErrorCode: "pow_failed");
    string normalized = this._phoneUtil.NormalizeToE164(request.Phone);
    string phoneHash = this._crypto.HashPhone(normalized);
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    (int limit, int windowSeconds) = this._otpRateLimitOptions.Value.GetPerMinuteLimit();
    (bool flag, int RetryAfterSeconds) = await this._rateLimitService.CheckRateLimitAsync("otp:min:" + phoneHash, limit, windowSeconds, cancellationToken);
    if (!flag)
      return new RequestOtpResponse(false, RetryAfterSeconds, ErrorCode: "rate_limit_exceeded");
    AuthKnownUserEntity byPhoneHashAsync = await this._authDataService.GetKnownUserByPhoneHashAsync(phoneHash, cancellationToken);
    if (byPhoneHashAsync != null && !byPhoneHashAsync.IsActive)
    {
      this._logger.LogWarning("OTP request rejected: user inactive (business deactivation). UserId={UserId}, PhoneHash={PhoneHash}", (object) byPhoneHashAsync.UserId, (object) phoneHash);
      this._metrics.IncOtpRequest();
      return new RequestOtpResponse(false, ErrorCode: "user_inactive", UserExists: true);
    }
    AuthUserEntity user = await this._authDataService.GetUserByPhoneHashAsync(phoneHash, cancellationToken);
    if (user != null && user.IsBlocked)
    {
      this._logger.LogWarning("OTP request rejected: user blocked (security). UserId={UserId}, Reason={Reason}", (object) user.Id, (object) user.BlockReason);
      this._metrics.IncOtpRequest();
      return new RequestOtpResponse(false, ErrorCode: "user_blocked", UserExists: true);
    }
    OtpChallengeEntity otpChallengeAsync1 = await this._authDataService.GetLatestOtpChallengeAsync(phoneHash, request.DeviceId, cancellationToken);
    if (otpChallengeAsync1 != null && otpChallengeAsync1.CreatedAt.AddSeconds((double) windowSeconds) > now)
      return new RequestOtpResponse(false, (int) Math.Ceiling((otpChallengeAsync1.CreatedAt.AddSeconds((double) windowSeconds) - now).TotalSeconds), ErrorCode: "too_soon", UserExists: user != null);
    OtpOptions currentValue = this._otpOptions.CurrentValue;
    string code = RequestOtpCommandHandler.GenerateCode(currentValue.Digits);
    DateTime dateTime = now.AddSeconds((double) currentValue.TtlSeconds);
    string channel = "sms";
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
      IssuerIp = request.ClientIp ?? "unknown",
      IssuerDeviceId = request.DeviceId,
      DeliveryChannel = channel,
      DeliveryStatus = "pending"
    };
    OtpChallengeEntity otpChallengeAsync2 = await this._authDataService.CreateOtpChallengeAsync(challenge, cancellationToken);
    this._metrics.IncOtpRequest(status: "delivered");
    this._otpDeliveryService.DeliverAsync(normalized, code, channel, cancellationToken);
    IAuthDataService authDataService = this._authDataService;
    AuditLogEntity log = new AuditLogEntity();
    log.Id = Guid.NewGuid();
    log.UserId = user?.Id;
    log.PhoneHash = phoneHash;
    log.Action = "otp_request";
    log.Result = "success";
    log.CreatedAt = now;
    log.Ip = request.ClientIp;
    log.UserAgent = request.UserAgent;
    log.DeviceId = request.DeviceId;
    log.MetadataJson = JsonSerializer.Serialize(new
    {
      challengeId = challenge.Id,
      type = challenge.ChallengeType
    });
    CancellationToken ct = cancellationToken;
    await authDataService.CreateAuditLogAsync(log, ct);
    var exposeDebugCode = this._environment.IsDevelopment() || currentValue.ExposeDebugCode;
    return new RequestOtpResponse(true, Code: exposeDebugCode ? code : null, UserExists: user != null, SelectedChannel: channel);
  }

  private static string GenerateCode(int digits)
  {
    digits = Math.Clamp(digits, 4, 10);
    int int32 = RandomNumberGenerator.GetInt32(0, (int) Math.Pow(10.0, (double) digits));
    // ISSUE: explicit reference operation
    return int32.ToString($"D{digits}");
  }
}
