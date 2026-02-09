// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.LoginWithPassword.LoginWithPasswordCommandHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using RPlus.Auth.Domain.Entities;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Eventing.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.LoginWithPassword;

public class LoginWithPasswordCommandHandler : 
  IRequestHandler<LoginWithPasswordCommand, LoginWithPasswordResponse>
{
  private readonly IAuthDataService _authDataService;
  private readonly ICryptoService _crypto;
  private readonly ITokenService _tokenService;
  private readonly IPhoneUtil _phoneUtil;
  private readonly IRedisRateLimitService _rateLimitService;
  private readonly IProtectionService _protectionService;
  private readonly ISystemAuthFlowStore _systemAuthFlowStore;
  private readonly ILogger<LoginWithPasswordCommandHandler> _logger;
  private readonly TimeProvider _timeProvider;
  private readonly IEventPublisher _eventPublisher;

  public LoginWithPasswordCommandHandler(
    IAuthDataService authDataService,
    ICryptoService crypto,
    ITokenService tokenService,
    IPhoneUtil phoneUtil,
    IRedisRateLimitService rateLimitService,
    IProtectionService protectionService,
    ISystemAuthFlowStore systemAuthFlowStore,
    ILogger<LoginWithPasswordCommandHandler> logger,
    TimeProvider timeProvider,
    IEventPublisher eventPublisher)
  {
    this._authDataService = authDataService;
    this._crypto = crypto;
    this._tokenService = tokenService;
    this._phoneUtil = phoneUtil;
    this._rateLimitService = rateLimitService;
    this._protectionService = protectionService;
    this._systemAuthFlowStore = systemAuthFlowStore;
    this._logger = logger;
    this._timeProvider = timeProvider;
    this._eventPublisher = eventPublisher;
  }

  public async Task<LoginWithPasswordResponse> Handle(
    LoginWithPasswordCommand request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Nonce))
      return new LoginWithPasswordResponse(false, null, null, null, null, "pow_missing");
    if (!await this._protectionService.VerifySolutionAsync(request.ChallengeId, request.Nonce, request.ClientIp, cancellationToken))
      return new LoginWithPasswordResponse(false, null, null, null, null, "pow_failed");

    string trimmedPhone = request.Phone.Trim();
    string? phoneHash = null;
    string? identifierHash = null;
    AuthUserEntity? user = null;
    try
    {
      string normalizedPhone = this._phoneUtil.NormalizeToE164(trimmedPhone);
      phoneHash = this._crypto.HashPhone(normalizedPhone);
      user = await this._authDataService.GetUserByPhoneHashAsync(phoneHash, cancellationToken);
    }
    catch (ArgumentException)
    {
      user = await this._authDataService.GetUserByIdentifierAsync(trimmedPhone, cancellationToken);
      identifierHash = "id:" + trimmedPhone;
    }
    string rateLimitSuffix = phoneHash ?? identifierHash ?? trimmedPhone;
    string rateLimitKey = "login:pwd:" + rateLimitSuffix;
    (bool allowed, int retryAfterSeconds) = await this._rateLimitService.CheckRateLimitAsync(rateLimitKey, 5, 900, cancellationToken);
    if (!allowed)
    {
      this._logger.LogWarning("Login rate limit exceeded for {Key}", (object) rateLimitKey);
      return new LoginWithPasswordResponse(false, null, null, null, null, "rate_limit_exceeded", retryAfterSeconds);
    }
    if (user == null)
    {
      this._crypto.HashPassword("dummy", this._crypto.GenerateSalt());
      await this.PublishLoginFailed(null, trimmedPhone, "invalid_credentials", request.ClientIp, request.UserAgent, cancellationToken);
      return new LoginWithPasswordResponse(false, null, null, null, null, "invalid_credentials");
    }
    if (user.IsBlocked)
    {
      await this.PublishLoginFailed(user.Id.ToString(), trimmedPhone, "user_blocked", request.ClientIp, request.UserAgent, cancellationToken);
      return new LoginWithPasswordResponse(false, null, null, null, null, "user_blocked");
    }
    AuthCredentialEntity? credential = await this._authDataService.GetAuthCredentialAsync(user.Id, cancellationToken);
    if (credential == null)
    {
      this._crypto.HashPassword("dummy", this._crypto.GenerateSalt());
      await this.PublishLoginFailed(user.Id.ToString(), trimmedPhone, "password_not_set", request.ClientIp, request.UserAgent, cancellationToken);
      return new LoginWithPasswordResponse(false, null, null, null, null, "password_not_set");
    }
    if (!this._crypto.VerifyPassword(request.Password, credential.PasswordHash, credential.PasswordSalt))
    {
      this._logger.LogWarning("Invalid password for UserId={UserId}", (object) user.Id);
      await this.PublishLoginFailed(user.Id.ToString(), trimmedPhone, "invalid_credentials", request.ClientIp, request.UserAgent, cancellationToken);
      return new LoginWithPasswordResponse(false, null, null, null, null, "invalid_credentials");
    }

    // For system users we enforce a security setup wizard (password change + email recovery + mandatory 2FA).
    // We validate the password here, but we do not issue access/refresh tokens until setup is complete.
    string deviceKey = request.DeviceId ?? Guid.NewGuid().ToString();
    if (user.IsSystem)
    {
      string tempToken = await this._systemAuthFlowStore.CreateAsync(
        new SystemAuthFlow(
          user.Id,
          deviceKey,
          NormalizeIp(request.ClientIp),
          request.UserAgent ?? string.Empty,
          this._timeProvider.GetUtcNow().UtcDateTime),
        TimeSpan.FromMinutes(10),
        cancellationToken);

      string? emailMask = MaskEmail(user.RecoveryEmail);

      if (user.RequiresPasswordChange)
        return new LoginWithPasswordResponse(false, null, null, null, null, null, null, user.Id.ToString(), "CHANGE_PASSWORD", tempToken, emailMask);

      if (string.IsNullOrWhiteSpace(user.RecoveryEmail))
        return new LoginWithPasswordResponse(false, null, null, null, null, null, null, user.Id.ToString(), "SETUP_EMAIL", tempToken, null);

      if (!user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
        return new LoginWithPasswordResponse(false, null, null, null, null, null, null, user.Id.ToString(), "SETUP_2FA", tempToken, emailMask);

      return new LoginWithPasswordResponse(false, null, null, null, null, null, null, user.Id.ToString(), "ENTER_TOTP", tempToken, emailMask);
    }

    DeviceEntity? device = await this._authDataService.GetDeviceAsync(deviceKey, user.Id, cancellationToken);
    DateTime currentTimestamp = this._timeProvider.GetUtcNow().UtcDateTime;
    if (device == null)
    {
      device = new DeviceEntity()
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        DeviceKey = deviceKey,
        CreatedAt = currentTimestamp,
        LastSeenAt = currentTimestamp,
        IsBlocked = false
      };
      await this._authDataService.CreateDeviceAsync(device, cancellationToken);
    }
    else
      device.LastSeenAt = currentTimestamp;
    TokenPair tokens = await this._tokenService.IssueTokensAsync(user, device, null, request.ClientIp, request.UserAgent, cancellationToken);
    AuditLogEntity log = new AuditLogEntity();
    log.Id = Guid.NewGuid();
    log.UserId = user.Id;
    log.PhoneHash = phoneHash;
    log.Action = "login_password";
    log.Result = "success";
    log.CreatedAt = this._timeProvider.GetUtcNow().UtcDateTime;
    log.Ip = NormalizeIp(request.ClientIp);
    log.UserAgent = request.UserAgent;
    log.DeviceId = deviceKey;
    await this._authDataService.CreateAuditLogAsync(log, cancellationToken);
    string aggregateId = user.Id.ToString();
    string ipAddress = NormalizeIp(request.ClientIp);
    DateTime loginTimestamp = this._timeProvider.GetUtcNow().UtcDateTime;
    try
    {
      await this._eventPublisher.PublishAsync<UserLoggedIn>(new UserLoggedIn(aggregateId, deviceKey, "Password", ipAddress, request.UserAgent, loginTimestamp), "auth.user.logged_in.v1", aggregateId, cancellationToken);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish UserLoggedIn event");
    }
    return new LoginWithPasswordResponse(true, tokens.AccessToken, tokens.RefreshToken, tokens.AccessExpiresAt, tokens.RefreshExpiresAt, null, null, user.Id.ToString());
  }

  private async Task PublishLoginFailed(
    string? userId,
    string identifier,
    string reason,
    string? ip,
    string? ua,
    CancellationToken ct)
  {
    string safeIp = NormalizeIp(ip);
    UserLoginFailed payload = new UserLoginFailed(userId, identifier, reason, safeIp, ua, this._timeProvider.GetUtcNow().UtcDateTime);
    string aggregateId = userId ?? "anonymous";
    try
    {
      await this._eventPublisher.PublishAsync<UserLoginFailed>(payload, "auth.user.login_failed.v1", aggregateId, ct);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish UserLoginFailed event");
    }
  }

  private static string NormalizeIp(string? ip) => string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;

  private static string? MaskEmail(string? email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return null;

    var at = email.IndexOf('@');
    if (at <= 1)
      return "***";

    var local = email.Substring(0, at);
    var domain = email.Substring(at + 1);
    var first = local.Substring(0, 1);
    return $"{first}***@{domain}";
  }
}
