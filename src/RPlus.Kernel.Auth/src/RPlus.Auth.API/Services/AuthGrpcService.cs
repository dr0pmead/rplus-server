using Grpc.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Contracts.Events;
using RPlus.SDK.Auth.Commands;
using RPlus.SDK.Auth.Enums;
using RPlus.SDK.Auth.Queries;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using RPlusGrpc.Auth;
using GrpcRefreshTokenResponse = RPlusGrpc.Auth.RefreshTokenResponse;
using GrpcRequestOtpResponse = RPlusGrpc.Auth.RequestOtpResponse;
using SdkRefreshTokenResponse = RPlus.SDK.Auth.Commands.RefreshTokenResponse;
using SdkRequestOtpResponse = RPlus.SDK.Auth.Commands.RequestOtpResponse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

#nullable enable
namespace RPlus.Auth.Api.Services;

public class AuthGrpcService : AuthService.AuthServiceBase
{
    private readonly IMediator _mediator;
    private readonly ITokenService _tokenService;
    private readonly ISystemAuthFlowStore _systemFlowStore;
    private readonly ICryptoService _crypto;
    private readonly IVaultCryptoService _vault;
    private readonly ITotpService _totp;
    private readonly IEventPublisher _events;
    private readonly TimeProvider _timeProvider;
    private readonly IHostEnvironment _environment;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<AuthGrpcService> _logger;
    private readonly AuthDbContext _db;

    public AuthGrpcService(
        IMediator mediator,
        ITokenService tokenService,
        ISystemAuthFlowStore systemFlowStore,
        ICryptoService crypto,
        IVaultCryptoService vault,
        ITotpService totp,
        IEventPublisher events,
        TimeProvider timeProvider,
        IHostEnvironment environment,
        IConnectionMultiplexer redis,
        ILogger<AuthGrpcService> logger,
        AuthDbContext db)
    {
        _mediator = mediator;
        _tokenService = tokenService;
        _systemFlowStore = systemFlowStore;
        _crypto = crypto;
        _vault = vault;
        _totp = totp;
        _events = events;
        _timeProvider = timeProvider;
        _environment = environment;
        _redis = redis;
        _logger = logger;
        _db = db;
        _logger.LogInformation("AuthGrpcService instantiated and ready for gRPC calls.");
    }

    private static bool IsTruthyEnv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim() is "1" or "true" or "TRUE" or "True" or "yes" or "YES" or "Yes" or "on" or "ON" or "On";
    }

    private bool ShouldLogDevOtpCodes()
        => _environment.IsDevelopment() || IsTruthyEnv(Environment.GetEnvironmentVariable("AUTH_DEV_OTP_LOG"));

    public override async Task<IdentifyUserResponse> IdentifyUser(
        IdentifyUserRequest request,
        ServerCallContext context)
    {
        IdentifyUserResult result = await _mediator.Send(new IdentifyUserQuery(request.Identifier, request.ClientIp, request.UserAgent), context.CancellationToken);
        return new IdentifyUserResponse
        {
            Exists = result.Exists,
            AuthMethod = result.AuthMethod ?? string.Empty,
            IsBlocked = result.IsBlocked
        };
    }

    public override async Task<LoginResponse> LoginWithPassword(
        LoginWithPasswordRequest request,
        ServerCallContext context)
    {
        LoginWithPasswordResponse result = await _mediator.Send(new LoginWithPasswordCommand(
            request.Login, request.Password, request.DeviceId, null, request.ClientIp, request.UserAgent, request.ChallengeId, request.Nonce));
        
        return new LoginResponse
        {
            Success = result.Success,
            AccessToken = result.AccessToken ?? "",
            RefreshToken = result.RefreshToken ?? "",
            Error = result.Error ?? "",
            RetryAfterSeconds = result.RetryAfterSeconds.GetValueOrDefault(),
            UserId = result.UserId ?? "",
            NextAction = result.NextAction ?? "",
            TempToken = result.TempToken ?? "",
            RecoveryEmailMask = result.RecoveryEmailMask ?? ""
        };
    }

    public override async Task<SystemAdminStepResponse> SystemAdminChangePassword(
        SystemAdminChangePasswordRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.NewPassword))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem)
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        if (request.NewPassword.Length < 12)
            return new SystemAdminStepResponse { Success = false, Error = "password_too_short" };

        var cred = await _db.AuthCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, context.CancellationToken);
        if (cred is null)
            return new SystemAdminStepResponse { Success = false, Error = "password_not_set" };

        var salt = _crypto.GenerateSalt();
        var hash = _crypto.HashPassword(request.NewPassword, salt);
        cred.PasswordSalt = salt;
        cred.PasswordHash = hash;
        cred.ChangedAt = _timeProvider.GetUtcNow().UtcDateTime;

        user.RequiresPasswordChange = false;
        await _db.SaveChangesAsync(context.CancellationToken);

        return await BuildNextSystemStepAsync(user, request.TempToken, flow, context.CancellationToken);
    }

    public override async Task<SystemAdminStepResponse> SystemAdminUpdateProfile(
        SystemAdminUpdateProfileRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem)
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        var login = (request.Login ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(login))
        {
            login = login.ToLowerInvariant();

            if (login.Length < 3 || login.Length > 64)
                return new SystemAdminStepResponse { Success = false, Error = "invalid_login" };

            for (var i = 0; i < login.Length; i++)
            {
                var ch = login[i];
                var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch is '.' or '_' or '-';
                if (!ok)
                    return new SystemAdminStepResponse { Success = false, Error = "invalid_login" };
            }

            var exists = await _db.AuthUsers.AsNoTracking().AnyAsync(u => u.Login == login && u.Id != user.Id, context.CancellationToken);
            if (exists)
                return new SystemAdminStepResponse { Success = false, Error = "login_taken" };

            user.Login = login;
        }

        var recoveryEmail = (request.RecoveryEmail ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(recoveryEmail))
        {
            if (!recoveryEmail.Contains("@", StringComparison.Ordinal) || recoveryEmail.Length > 320)
                return new SystemAdminStepResponse { Success = false, Error = "invalid_email" };

            // MVP: direct set (no out-of-band confirmation). Recovery flow is dev-log mode.
            user.RecoveryEmail = recoveryEmail;
        }

        await _db.SaveChangesAsync(context.CancellationToken);
        return await BuildNextSystemStepAsync(user, request.TempToken, flow, context.CancellationToken);
    }

    public override async Task<SystemAdminStepResponse> SystemAdminSetRecoveryEmail(
        SystemAdminSetRecoveryEmailRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Email))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem)
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        var email = request.Email.Trim();
        if (!email.Contains("@", StringComparison.Ordinal) || email.Length > 320)
            return new SystemAdminStepResponse { Success = false, Error = "invalid_email" };

        // Email confirmation (MVP / Dev-log mode):
        // - When request.code is empty => generate/store code and log it in Development (no Kafka).
        // - When request.code is present => verify and persist RecoveryEmail, then advance wizard.
        var code = (request.Code ?? string.Empty).Trim();
        var redisDb = _redis.GetDatabase();
        var codeKey = $"auth:sysadmin:email:{request.TempToken}";

        if (string.IsNullOrWhiteSpace(code))
        {
            var generated = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            await redisDb.StringSetAsync(codeKey, generated, TimeSpan.FromMinutes(10));

            if (ShouldLogDevOtpCodes())
            {
                _logger.LogWarning(">>> [DEV OTP] Email setup code for {Email}: {Code} <<<", email, generated);
            }

            // Stay on SETUP_EMAIL in UI (no NextAction) — frontend will ask the user to input the code.
            return new SystemAdminStepResponse
            {
                Success = true,
                TempToken = request.TempToken,
                RecoveryEmailMask = MaskEmail(email) ?? "",
                UserId = user.Id.ToString()
            };
        }

        var expected = await redisDb.StringGetAsync(codeKey);
        if (!expected.HasValue || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected!), Encoding.UTF8.GetBytes(code)))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_code" };

        user.RecoveryEmail = email;
        await _db.SaveChangesAsync(context.CancellationToken);
        await redisDb.KeyDeleteAsync(codeKey);

        return await BuildNextSystemStepAsync(user, request.TempToken, flow, context.CancellationToken);
    }

    public override async Task<SystemAdminStepResponse> SystemAdminSetup2fa(
        SystemAdminSetup2faRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem)
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        if (string.IsNullOrWhiteSpace(user.RecoveryEmail))
            return new SystemAdminStepResponse { Success = false, Error = "recovery_email_missing" };

        var secret = _totp.GenerateSecretBase32();
        user.TotpSecretEncrypted = await _vault.EncryptToBase64Async(secret, context.CancellationToken);
        user.IsTwoFactorEnabled = false;
        await _db.SaveChangesAsync(context.CancellationToken);

        var uri = _totp.BuildOtpAuthUri("RPlus", user.Login ?? "admin", secret);
        return new SystemAdminStepResponse
        {
            Success = true,
            TempToken = request.TempToken,
            NextAction = "SETUP_2FA",
            RecoveryEmailMask = MaskEmail(user.RecoveryEmail) ?? "",
            OtpauthUri = uri,
            UserId = user.Id.ToString()
        };
    }

    public override async Task<SystemAdminStepResponse> SystemAdminVerify2fa(
        SystemAdminVerify2faRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Code))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem)
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        if (string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
            return new SystemAdminStepResponse { Success = false, Error = "totp_not_initialized" };

        var secret = await _vault.DecryptFromBase64Async(user.TotpSecretEncrypted, context.CancellationToken);
        var ok = await _totp.VerifyAsync(secret, request.Code, context.CancellationToken);
        if (!ok)
            return new SystemAdminStepResponse { Success = false, Error = "invalid_totp" };

        user.IsTwoFactorEnabled = true;
        user.RequiresSetup = false;
        await _db.SaveChangesAsync(context.CancellationToken);

        var tokens = await IssueTokensFromFlowAsync(user, flow, context.CancellationToken);
        await _systemFlowStore.DeleteAsync(request.TempToken, context.CancellationToken);

        return new SystemAdminStepResponse
        {
            Success = true,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            UserId = user.Id.ToString()
        };
    }

    public override async Task<SystemAdminStepResponse> SystemAdminVerifyTotp(
        SystemAdminVerifyTotpRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Code))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem || !user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        var secret = await _vault.DecryptFromBase64Async(user.TotpSecretEncrypted, context.CancellationToken);
        var ok = await _totp.VerifyAsync(secret, request.Code, context.CancellationToken);
        if (!ok)
            return new SystemAdminStepResponse { Success = false, Error = "invalid_totp" };

        var tokens = await IssueTokensFromFlowAsync(user, flow, context.CancellationToken);
        await _systemFlowStore.DeleteAsync(request.TempToken, context.CancellationToken);

        return new SystemAdminStepResponse
        {
            Success = true,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            UserId = user.Id.ToString()
        };
    }

    public override async Task<SystemAdminStepResponse> SystemAdminRecoveryInit(
        SystemAdminRecoveryInitRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem || string.IsNullOrWhiteSpace(user.RecoveryEmail))
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var redisDb = _redis.GetDatabase();
        await redisDb.StringSetAsync($"auth:sysadmin:recovery:{request.TempToken}", code, TimeSpan.FromMinutes(10));

        // MVP / Dev-log mode: no notifications integration yet.
        if (ShouldLogDevOtpCodes())
        {
            _logger.LogWarning(">>> [DEV OTP] Recovery code for {Email}: {Code} <<<", user.RecoveryEmail, code);
        }

        return new SystemAdminStepResponse
        {
            Success = true,
            TempToken = request.TempToken,
            RecoveryEmailMask = MaskEmail(user.RecoveryEmail) ?? "",
            DebugCode = ""
        };
    }

    public override async Task<SystemAdminStepResponse> SystemAdminRecoveryVerify(
        SystemAdminRecoveryVerifyRequest request,
        ServerCallContext context)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Code))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_request" };

        var flow = await _systemFlowStore.GetAsync(request.TempToken, context.CancellationToken);
        if (flow is null)
            return new SystemAdminStepResponse { Success = false, Error = "temp_token_invalid" };

        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Id == flow.UserId, context.CancellationToken);
        if (user is null || !user.IsSystem || string.IsNullOrWhiteSpace(user.RecoveryEmail))
            return new SystemAdminStepResponse { Success = false, Error = "unauthorized" };

        var redisDb = _redis.GetDatabase();
        var expected = await redisDb.StringGetAsync($"auth:sysadmin:recovery:{request.TempToken}");
        if (!expected.HasValue || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected!), Encoding.UTF8.GetBytes(request.Code.Trim())))
            return new SystemAdminStepResponse { Success = false, Error = "invalid_code" };

        // Recovery = allow re-setup 2FA (do not issue tokens without 2FA).
        user.IsTwoFactorEnabled = false;
        user.TotpSecretEncrypted = null;
        user.RequiresSetup = true;
        await _db.SaveChangesAsync(context.CancellationToken);

        return new SystemAdminStepResponse
        {
            Success = false,
            TempToken = request.TempToken,
            NextAction = "SETUP_2FA",
            RecoveryEmailMask = MaskEmail(user.RecoveryEmail) ?? "",
            UserId = user.Id.ToString()
        };
    }

    private static Task<SystemAdminStepResponse> BuildNextSystemStepAsync(
        AuthUserEntity user,
        string tempToken,
        SystemAuthFlow flow,
        CancellationToken ct)
    {
        var emailMask = MaskEmail(user.RecoveryEmail) ?? "";

        if (user.RequiresPasswordChange)
            return Task.FromResult(new SystemAdminStepResponse { Success = false, TempToken = tempToken, NextAction = "CHANGE_PASSWORD", UserId = user.Id.ToString(), RecoveryEmailMask = emailMask });

        if (string.IsNullOrWhiteSpace(user.RecoveryEmail))
            return Task.FromResult(new SystemAdminStepResponse { Success = false, TempToken = tempToken, NextAction = "SETUP_EMAIL", UserId = user.Id.ToString() });

        if (!user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
            return Task.FromResult(new SystemAdminStepResponse { Success = false, TempToken = tempToken, NextAction = "SETUP_2FA", UserId = user.Id.ToString(), RecoveryEmailMask = emailMask });

        return Task.FromResult(new SystemAdminStepResponse { Success = false, TempToken = tempToken, NextAction = "ENTER_TOTP", UserId = user.Id.ToString(), RecoveryEmailMask = emailMask });
    }

    private async Task<TokenPair> IssueTokensFromFlowAsync(AuthUserEntity user, SystemAuthFlow flow, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.UserId == user.Id && d.DeviceKey == flow.DeviceId, ct);
        if (device is null)
        {
            device = new DeviceEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceKey = flow.DeviceId,
                CreatedAt = now,
                LastSeenAt = now,
                IsBlocked = false
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            device.LastSeenAt = now;
            await _db.SaveChangesAsync(ct);
        }

        return await _tokenService.IssueTokensAsync(user, device, null, flow.ClientIp, flow.UserAgent, ct);
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var at = email.IndexOf('@');
        if (at <= 1)
            return "***";

        var local = email.Substring(0, at);
        var domain = email.Substring(at + 1);
        return $"{local.Substring(0, 1)}***@{domain}";
    }

    public override async Task<GrpcRequestOtpResponse> RequestOtp(
        RequestOtpRequest request,
        ServerCallContext context)
    {
        var challengeId = TryGetMetadata(context, "pow-challenge-id") ?? TryGetMetadata(context, "x-pow-challenge-id");
        var nonce = TryGetMetadata(context, "pow-nonce") ?? TryGetMetadata(context, "x-pow-nonce");

        SdkRequestOtpResponse result = await _mediator.Send(new RequestOtpCommand(
            request.Phone,
            request.DeviceId,
            request.ClientIp,
            request.UserAgent,
            request.Channel,
            challengeId,
            nonce));
             
        return new GrpcRequestOtpResponse
        {
            Sent = result.Success,
            RetryAfterSeconds = result.RetryAfterSeconds,
            DebugCode = result.Code ?? "",
            Error = result.ErrorCode ?? "",
            UserExists = result.UserExists,
            SelectedChannel = result.SelectedChannel ?? ""
        };
    }

    private static string? TryGetMetadata(ServerCallContext context, string key)
    {
        if (context?.RequestHeaders is null)
            return null;

        foreach (var entry in context.RequestHeaders)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return null;
    }

    public override async Task<LoginResponse> VerifyOtp(
        VerifyOtpRequest request,
        ServerCallContext context)
    {
        VerifyOtpResponse result = await _mediator.Send(new VerifyOtpCommand(
            request.Phone, request.Code, request.DeviceId, request.DevicePublicKey, request.ClientIp, request.UserAgent));

        if (result.Status != OtpVerifyStatus.Success)
        {
            return new LoginResponse
            {
                Success = false,
                Error = result.Status.ToString()
            };
        }

        TokenPair tokens = await _tokenService.IssueTokensAsync(
            (AuthUserEntity)result.User!, (DeviceEntity)result.Device!, request.DevicePublicKey, request.ClientIp, request.UserAgent, context.CancellationToken);

        return new LoginResponse
        {
            Success = true,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            UserId = result.User!.Id.ToString()
        };
    }

    public override async Task<GrpcRefreshTokenResponse> RefreshToken(
        RefreshTokenRequest request,
        ServerCallContext context)
    {
        SdkRefreshTokenResponse result = await _mediator.Send(new RefreshTokenCommand(
            request.RefreshToken, request.DeviceId, request.DevicePublicKey, request.ClientIp, request.UserAgent));

        if (!result.Success)
        {
            return new GrpcRefreshTokenResponse
            {
                Success = false,
                Error = result.ErrorCode ?? "refresh_failed"
            };
        }

        TokenPair tokens = await _tokenService.IssueTokensAsync(
            (AuthUserEntity)result.User!, (DeviceEntity)result.Device!, request.DevicePublicKey, request.ClientIp, request.UserAgent, context.CancellationToken);

        return new GrpcRefreshTokenResponse
        {
            Success = true,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = (int)(tokens.AccessExpiresAt - DateTime.UtcNow).TotalSeconds
        };
    }

    public override async Task<LogoutResponse> Logout(
        LogoutRequest request,
        ServerCallContext context)
    {
        await _tokenService.RevokeAsync(request.RefreshToken, request.DeviceId, context.CancellationToken);
        return new LogoutResponse { Success = true };
    }

    public override async Task<ListAuthUsersResponse> ListAuthUsers(
        ListAuthUsersRequest request,
        ServerCallContext context)
    {
        List<Guid> ids = request.UserIds
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        if (!ids.Any()) return new ListAuthUsersResponse();

        var users = await _db.AuthUsers
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(context.CancellationToken);

        var response = new ListAuthUsersResponse();
        response.Users.AddRange(users.Select(u => new AuthUserInfo
        {
            UserId = u.Id.ToString(),
            Login = u.Login ?? "",
            Email = u.Email ?? "",
            PhoneHash = u.PhoneHash,
            IsBlocked = u.IsBlocked,
            IsActive = !u.IsBlocked
        }));

        return response;
    }

    public override async Task<CreateUserResponse> CreateUser(
        RPlusGrpc.Auth.CreateUserRequest request,
        ServerCallContext context)
    {
        var userType = request.UserType switch
        {
            1 => RPlus.SDK.Auth.Enums.AuthUserType.Platform,
            2 => RPlus.SDK.Auth.Enums.AuthUserType.Staff,
            _ => RPlus.SDK.Auth.Enums.AuthUserType.Staff
        };

        var command = new RPlus.Auth.Application.Commands.CreateUser.CreateUserCommand(
            Login: request.Login,
            Email: request.Email,
            Phone: request.Phone,
            Password: request.Password,
            FirstName: string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName,
            LastName: string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName,
            MiddleName: string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName,
            UserType: userType,
            TenantId: Guid.TryParse(request.TenantId, out var tid) ? tid : null,
            Iin: null,
            BirthDate: null,
            HireDate: null,
            OrganizationNodeId: null,
            DivisionNodeId: null,
            DepartmentNodeId: null,
            PositionNodeId: null,
            HrCustomFields: null
        );

        var result = await _mediator.Send(command, context.CancellationToken);

        return new CreateUserResponse
        {
            Success = result.Success,
            UserId = result.UserId?.ToString() ?? "",
            ErrorCode = result.ErrorCode ?? ""
        };
    }

    public override async Task<TerminateUserResponse> TerminateUser(
        TerminateUserRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new TerminateUserResponse
            {
                Success = false,
                ErrorCode = "invalid_user_id"
            };
        }

        var command = new RPlus.Auth.Application.Commands.TerminateUser.TerminateUserCommand(
            UserId: userId,
            Reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason
        );

        var result = await _mediator.Send(command, context.CancellationToken);

        return new TerminateUserResponse
        {
            Success = result.Success,
            ErrorCode = result.ErrorCode ?? ""
        };
    }
}
