using Grpc.Core;
using GrpcStatusCode = Grpc.Core.StatusCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Gateway.Api.Auth;
using RPlusGrpc.Auth;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthGatewayController : ControllerBase
{
    private readonly AuthService.AuthServiceClient _authClient;
    private readonly ILogger<AuthGatewayController> _logger;
    private readonly IMemoryCache _cache;
    private readonly AuthCookieOptions _cookies;

    public AuthGatewayController(
        AuthService.AuthServiceClient authClient,
        ILogger<AuthGatewayController> logger,
        IMemoryCache cache,
        IOptions<AuthCookieOptions> cookies)
    {
        _authClient = authClient;
        _logger = logger;
        _cache = cache;
        _cookies = cookies.Value;
    }

    [HttpPost("identify")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-identify")]
    public async Task<IActionResult> Identify([FromBody] IdentifyUserRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            if (string.IsNullOrWhiteSpace(request.Identifier))
                return BadRequest(new { error = "invalid_request" });

            request.ClientIp = GetClientIpAddress();
            request.UserAgent = GetUserAgent();

            var cacheKey = BuildIdentifyCacheKey(request.ClientIp, request.Identifier);
            if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is CachedIdentifyResult cached)
            {
                return Ok(new
                {
                    exists = cached.Exists,
                    authMethod = cached.AuthMethod,
                    isBlocked = cached.IsBlocked
                });
            }

            var response = await _authClient.IdentifyUserAsync(request, cancellationToken: ct);
            var authMethod = NormalizeAuthMethod(response.AuthMethod, request.Identifier);

            _cache.Set(
                cacheKey,
                new CachedIdentifyResult(response.Exists, authMethod, response.IsBlocked),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) });

            return Ok(new
            {
                exists = response.Exists,
                authMethod,
                isBlocked = response.IsBlocked
            });
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "IdentifyUser");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy IdentifyUser to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("login/password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> LoginPassword([FromBody] LoginWithPasswordRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            request.ClientIp = GetClientIpAddress();
            request.UserAgent = GetUserAgent();

            var response = await _authClient.LoginWithPasswordAsync(request, cancellationToken: ct);
            if (!response.Success && !string.IsNullOrWhiteSpace(response.NextAction) && !string.IsNullOrWhiteSpace(response.TempToken))
            {
                // Persist device id early so subsequent wizard steps can complete cookie-based auth,
                // even though access/refresh tokens are not issued yet.
                SetDeviceIdCookie(request.DeviceId);

                return WizardActionRequired(new
                {
                    success = false,
                    action = response.NextAction,
                    tempToken = response.TempToken,
                    recoveryEmailMask = response.RecoveryEmailMask,
                    userId = response.UserId
                });
            }
            if (response.Success && !string.IsNullOrWhiteSpace(response.AccessToken))
            {
                // LoginResponse does not include expires_in; use configured cookie lifetime for access token.
                SetAuthCookies(response.AccessToken, response.RefreshToken, request.DeviceId, expiresInSeconds: 0);
            }
            if (IsCookieAuthMode())
            {
                return Ok(new
                {
                    success = response.Success,
                    retryAfterSeconds = response.RetryAfterSeconds,
                    userId = response.UserId,
                    error = response.Error
                });
            }

            return Ok(response);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "LoginWithPassword");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy LoginWithPassword to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    // Preferred endpoint for password-based login wizard (keeps URL stable as /api/auth/login).
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public Task<IActionResult> Login([FromBody] LoginWithPasswordRequest request, CancellationToken ct)
        => LoginPassword(request, ct);

    public sealed record SystemChangePasswordHttpRequest(string TempToken, string NewPassword);
    public sealed record SystemUpdateProfileHttpRequest(string TempToken, string? Login, string? RecoveryEmail);
    public sealed record SystemSetRecoveryEmailHttpRequest(string TempToken, string Email, string? Code);
    public sealed record SystemVerifyCodeHttpRequest(string TempToken, string Code);
    public sealed record SystemTempTokenHttpRequest(string TempToken);

    [HttpPost("system/change-password")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemChangePassword([FromBody] SystemChangePasswordHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminChangePasswordAsync(
                new SystemAdminChangePasswordRequest { TempToken = request.TempToken, NewPassword = request.NewPassword },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminChangePassword");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminChangePassword to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/profile")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemUpdateProfile([FromBody] SystemUpdateProfileHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminUpdateProfileAsync(
                new SystemAdminUpdateProfileRequest
                {
                    TempToken = request.TempToken,
                    Login = request.Login ?? string.Empty,
                    RecoveryEmail = request.RecoveryEmail ?? string.Empty
                },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminUpdateProfile");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminUpdateProfile to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/email")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemSetEmail([FromBody] SystemSetRecoveryEmailHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminSetRecoveryEmailAsync(
                new SystemAdminSetRecoveryEmailRequest { TempToken = request.TempToken, Email = request.Email, Code = request.Code ?? string.Empty },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminSetRecoveryEmail");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminSetRecoveryEmail to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/2fa/setup")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemSetup2fa([FromBody] SystemTempTokenHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminSetup2faAsync(
                new SystemAdminSetup2faRequest { TempToken = request.TempToken },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminSetup2fa");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminSetup2fa to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/2fa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemVerify2fa([FromBody] SystemVerifyCodeHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminVerify2faAsync(
                new SystemAdminVerify2faRequest { TempToken = request.TempToken, Code = request.Code },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminVerify2fa");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminVerify2fa to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/totp/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemVerifyTotp([FromBody] SystemVerifyCodeHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminVerifyTotpAsync(
                new SystemAdminVerifyTotpRequest { TempToken = request.TempToken, Code = request.Code },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminVerifyTotp");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminVerifyTotp to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/recovery/init")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemRecoveryInit([FromBody] SystemTempTokenHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminRecoveryInitAsync(
                new SystemAdminRecoveryInitRequest { TempToken = request.TempToken },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminRecoveryInit");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminRecoveryInit to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("system/recovery/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SystemRecoveryVerify([FromBody] SystemVerifyCodeHttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TempToken) || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { error = "invalid_request" });

            var res = await _authClient.SystemAdminRecoveryVerifyAsync(
                new SystemAdminRecoveryVerifyRequest { TempToken = request.TempToken, Code = request.Code },
                cancellationToken: ct);

            return MapSystemStepResponse(res);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "SystemAdminRecoveryVerify");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy SystemAdminRecoveryVerify to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    private IActionResult MapSystemStepResponse(SystemAdminStepResponse res)
    {
        if (res is null)
            return StatusCode(502, new { error = "upstream_error" });

        var deviceId =
            Request.Cookies.TryGetValue(_cookies.DeviceIdCookieName, out var did) && !string.IsNullOrWhiteSpace(did)
                ? did
                : (Request.Cookies.TryGetValue("device_id", out var legacyDid) ? legacyDid : string.Empty);

        if (res.Success && !string.IsNullOrWhiteSpace(res.AccessToken))
        {
            SetAuthCookies(res.AccessToken, res.RefreshToken, deviceId ?? string.Empty, expiresInSeconds: 0);
            return Ok(new { success = true, userId = res.UserId });
        }

        if (!string.IsNullOrWhiteSpace(res.NextAction) && !string.IsNullOrWhiteSpace(res.TempToken))
        {
            return WizardActionRequired(new
            {
                success = false,
                action = res.NextAction,
                tempToken = res.TempToken,
                recoveryEmailMask = res.RecoveryEmailMask,
                otpauthUri = res.OtpauthUri,
                debugCode = res.DebugCode,
                userId = res.UserId,
                error = res.Error
            });
        }

        if (!res.Success && !string.IsNullOrWhiteSpace(res.Error))
            return BadRequest(new { error = res.Error });

        return Ok(new
        {
            success = res.Success,
            tempToken = res.TempToken,
            recoveryEmailMask = res.RecoveryEmailMask,
            otpauthUri = res.OtpauthUri,
            debugCode = res.DebugCode,
            userId = res.UserId
        });
    }

    private void SetDeviceIdCookie(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        var now = DateTimeOffset.UtcNow;
        var domain = string.IsNullOrWhiteSpace(_cookies.Domain) ? null : _cookies.Domain;

        var deviceOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookies.Secure,
            SameSite = _cookies.SameSite,
            Domain = domain,
            Path = "/",
            Expires = now.AddDays(Math.Max(1, _cookies.RefreshDays))
        };

        Response.Cookies.Append(_cookies.DeviceIdCookieName, deviceId, deviceOptions);
        Response.Cookies.Append("device_id", deviceId, deviceOptions); // backward-compat
    }

    [HttpPost("otp/request")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-otp")]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpRequest request,
        [FromHeader(Name = "X-Pow-Challenge-Id")] string? powChallengeId,
        [FromHeader(Name = "X-Pow-Nonce")] string? powNonce,
        CancellationToken ct)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            request.ClientIp = GetClientIpAddress();
            request.UserAgent = GetUserAgent();

            Metadata? headers = null;
            if (!string.IsNullOrWhiteSpace(powChallengeId) && !string.IsNullOrWhiteSpace(powNonce))
            {
                headers = new Metadata
                {
                    { "pow-challenge-id", powChallengeId },
                    { "pow-nonce", powNonce }
                };
            }

            var response = await _authClient.RequestOtpAsync(request, headers: headers, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "RequestOtp");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy RequestOtp to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("otp/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            request.ClientIp = GetClientIpAddress();
            request.UserAgent = GetUserAgent();

            var response = await _authClient.VerifyOtpAsync(request, cancellationToken: ct);
            if (response.Success && !string.IsNullOrWhiteSpace(response.AccessToken))
            {
                // LoginResponse does not include expires_in; use configured cookie lifetime for access token.
                SetAuthCookies(response.AccessToken, response.RefreshToken, request.DeviceId, expiresInSeconds: 0);
            }
            if (IsCookieAuthMode())
            {
                return Ok(new
                {
                    success = response.Success,
                    retryAfterSeconds = response.RetryAfterSeconds,
                    userId = response.UserId,
                    error = response.Error
                });
            }

            return Ok(response);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "VerifyOtp");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy VerifyOtp to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request, CancellationToken ct)
    {
        try
        {
            request ??= new RefreshTokenRequest();

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                if (Request.Cookies.TryGetValue(_cookies.RefreshTokenCookieName, out var refresh) && !string.IsNullOrWhiteSpace(refresh))
                {
                    request.RefreshToken = refresh;
                }
                else if (Request.Cookies.TryGetValue("refresh_token", out var legacyRefresh) && !string.IsNullOrWhiteSpace(legacyRefresh))
                {
                    request.RefreshToken = legacyRefresh;
                }
            }

            if (string.IsNullOrWhiteSpace(request.DeviceId))
            {
                if (Request.Cookies.TryGetValue(_cookies.DeviceIdCookieName, out var deviceId) && !string.IsNullOrWhiteSpace(deviceId))
                {
                    request.DeviceId = deviceId;
                }
                else if (Request.Cookies.TryGetValue("device_id", out var legacyDeviceId) && !string.IsNullOrWhiteSpace(legacyDeviceId))
                {
                    request.DeviceId = legacyDeviceId;
                }
            }

            if (string.IsNullOrWhiteSpace(request.RefreshToken) || string.IsNullOrWhiteSpace(request.DeviceId))
                return Unauthorized(new { error = "unauthorized" });

            request.ClientIp = GetClientIpAddress();
            request.UserAgent = GetUserAgent();

            var response = await _authClient.RefreshTokenAsync(request, cancellationToken: ct);
            if (response.Success && !string.IsNullOrWhiteSpace(response.AccessToken))
            {
                SetAuthCookies(response.AccessToken, response.RefreshToken, request.DeviceId, response.ExpiresIn);
            }
            if (IsCookieAuthMode())
            {
                return Ok(new
                {
                    success = response.Success,
                    expiresIn = response.ExpiresIn,
                    error = response.Error
                });
            }

            return Ok(response);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "RefreshToken");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy RefreshToken to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        try
        {
            request ??= new LogoutRequest();

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                if (Request.Cookies.TryGetValue(_cookies.RefreshTokenCookieName, out var refresh) && !string.IsNullOrWhiteSpace(refresh))
                {
                    request.RefreshToken = refresh;
                }
                else if (Request.Cookies.TryGetValue("refresh_token", out var legacyRefresh) && !string.IsNullOrWhiteSpace(legacyRefresh))
                {
                    request.RefreshToken = legacyRefresh;
                }
            }

            if (string.IsNullOrWhiteSpace(request.DeviceId))
            {
                if (Request.Cookies.TryGetValue(_cookies.DeviceIdCookieName, out var deviceId) && !string.IsNullOrWhiteSpace(deviceId))
                {
                    request.DeviceId = deviceId;
                }
                else if (Request.Cookies.TryGetValue("device_id", out var legacyDeviceId) && !string.IsNullOrWhiteSpace(legacyDeviceId))
                {
                    request.DeviceId = legacyDeviceId;
                }
            }

            var response = await _authClient.LogoutAsync(request, cancellationToken: ct);
            ClearAuthCookies();
            return Ok(response);
        }
        catch (RpcException ex)
        {
            return HandleAuthRpcException(ex, "Logout");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy Logout to Auth gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    private string GetClientIpAddress()
    {
        // Do not trust client-supplied IP fields in the request body.
        // Configure Forwarded Headers middleware (with KnownNetworks/KnownProxies) when behind a reverse proxy.
        return HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private string GetUserAgent()
    {
        return HttpContext?.Request?.Headers.UserAgent.ToString() ?? string.Empty;
    }

    private void SetAuthCookies(string accessToken, string refreshToken, string deviceId, int expiresInSeconds)
    {
        var now = DateTimeOffset.UtcNow;

        DateTimeOffset? accessExpires = null;
        if (expiresInSeconds > 0)
        {
            accessExpires = now.AddSeconds(expiresInSeconds);
        }
        else if (_cookies.AccessMinutes > 0)
        {
            accessExpires = now.AddMinutes(_cookies.AccessMinutes);
        }

        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookies.Secure,
            SameSite = _cookies.SameSite,
            Domain = string.IsNullOrWhiteSpace(_cookies.Domain) ? null : _cookies.Domain,
            Path = "/",
            Expires = accessExpires
        };

        Response.Cookies.Append(_cookies.AccessTokenCookieName, accessToken, accessOptions);
        Response.Cookies.Append("access_token", accessToken, accessOptions); // backward-compat

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookies.Secure,
            SameSite = _cookies.SameSite,
            Domain = string.IsNullOrWhiteSpace(_cookies.Domain) ? null : _cookies.Domain,
            Path = "/",
            Expires = now.AddDays(Math.Max(1, _cookies.RefreshDays))
        };

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            Response.Cookies.Append(_cookies.RefreshTokenCookieName, refreshToken, refreshOptions);
            Response.Cookies.Append("refresh_token", refreshToken, refreshOptions); // backward-compat
        }

        var deviceOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookies.Secure,
            SameSite = _cookies.SameSite,
            Domain = string.IsNullOrWhiteSpace(_cookies.Domain) ? null : _cookies.Domain,
            Path = "/",
            Expires = now.AddDays(Math.Max(1, _cookies.RefreshDays))
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            Response.Cookies.Append(_cookies.DeviceIdCookieName, deviceId, deviceOptions);
            Response.Cookies.Append("device_id", deviceId, deviceOptions); // backward-compat
        }
    }

    private void ClearAuthCookies()
    {
        var domain = string.IsNullOrWhiteSpace(_cookies.Domain) ? null : _cookies.Domain;

        Response.Cookies.Delete(_cookies.AccessTokenCookieName, new CookieOptions { Path = "/", Domain = domain });
        Response.Cookies.Delete(_cookies.RefreshTokenCookieName, new CookieOptions { Path = "/", Domain = domain });
        Response.Cookies.Delete(_cookies.DeviceIdCookieName, new CookieOptions { Path = "/", Domain = domain });

        Response.Cookies.Delete("access_token", new CookieOptions { Path = "/", Domain = domain });
        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/", Domain = domain });
        Response.Cookies.Delete("device_id", new CookieOptions { Path = "/", Domain = domain });

        // Backward compatibility cleanup (host-only cookies)
        Response.Cookies.Delete(_cookies.AccessTokenCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(_cookies.RefreshTokenCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(_cookies.DeviceIdCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete("access_token", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("device_id", new CookieOptions { Path = "/" });
    }

    private bool IsCookieAuthMode()
    {
        var mode = Request.Headers["X-Auth-Mode"].ToString();
        if (string.IsNullOrWhiteSpace(mode))
            mode = Request.Headers["X-Auth-Transport"].ToString();

        return string.Equals(mode, "cookie", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIdentifyCacheKey(string? clientIp, string identifier)
    {
        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();
        var normalized = identifier.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return $"auth:identify:{ip}:{hash}";
    }

    private sealed record CachedIdentifyResult(bool Exists, string AuthMethod, bool IsBlocked);

    private bool PreferWizardOkResponses()
    {
        var requested = Request.Headers["X-Auth-Action-Response"].ToString();
        if (string.IsNullOrWhiteSpace(requested))
            return false;

        return string.Equals(requested, "200", StringComparison.OrdinalIgnoreCase)
               || string.Equals(requested, "ok", StringComparison.OrdinalIgnoreCase)
               || string.Equals(requested, "true", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult WizardActionRequired(object payload)
    {
        // "Action required" is part of the normal login/setup wizard and must not show up as a network error in browsers.
        // Return 200 and let the client drive the wizard based on `{ success:false, action, tempToken, ... }`.
        return Ok(payload);
    }

    private static string NormalizeAuthMethod(string value, string identifier)
    {
        if (string.Equals(value, "otp", StringComparison.OrdinalIgnoreCase))
            return "otp";

        if (string.Equals(value, "password", StringComparison.OrdinalIgnoreCase))
            return "password";

        // Backward/compat: some Auth builds return empty auth_method. For phone identifiers,
        // OTP is the only sensible default (supports both login and first-time registration).
        return LooksLikePhoneIdentifier(identifier) ? "otp" : "password";
    }

    private static bool LooksLikePhoneIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        // Accept E.164 (+<digits>) or common local formats with punctuation/spaces.
        var trimmed = identifier.Trim();
        var hasLeadingPlus = trimmed.StartsWith("+", StringComparison.Ordinal);

        var digits = 0;
        foreach (var ch in identifier)
        {
            if (char.IsDigit(ch))
                digits++;
        }

        // Typical phone ranges: 10..15 digits.
        if (digits is < 10 or > 15)
            return false;

        // Digits-only (optionally leading '+') should be treated as a phone identifier as well.
        // This matches common UI "sanitization" where formatting is removed before sending.
        var start = hasLeadingPlus ? 1 : 0;
        var isDigitsOnly = trimmed.Length > start;
        for (var i = start; i < trimmed.Length; i++)
        {
            if (!char.IsDigit(trimmed[i]))
            {
                isDigitsOnly = false;
                break;
            }
        }
        if (isDigitsOnly)
            return true;

        return hasLeadingPlus || identifier.Contains('(') || identifier.Contains(')') || identifier.Contains('-') || identifier.Contains(' ');
    }

    private IActionResult HandleAuthRpcException(RpcException ex, string operationName)
    {
        switch (ex.StatusCode)
        {
            case GrpcStatusCode.InvalidArgument:
                _logger.LogInformation(ex, "Auth gRPC rejected request (InvalidArgument) during {Operation}", operationName);
                return BadRequest(new { error = "invalid_request" });
            case GrpcStatusCode.Unauthenticated:
                _logger.LogInformation(ex, "Auth gRPC rejected request (Unauthenticated) during {Operation}", operationName);
                return Unauthorized(new { error = "unauthorized" });
            case GrpcStatusCode.PermissionDenied:
                _logger.LogInformation(ex, "Auth gRPC rejected request (PermissionDenied) during {Operation}", operationName);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
            case GrpcStatusCode.NotFound:
                _logger.LogInformation(ex, "Auth gRPC returned NotFound during {Operation}", operationName);
                return NotFound(new { error = "not_found" });
            case GrpcStatusCode.Unavailable:
                _logger.LogWarning(ex, "Auth gRPC unavailable during {Operation}", operationName);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });
            case GrpcStatusCode.DeadlineExceeded:
                _logger.LogWarning(ex, "Auth gRPC deadline exceeded during {Operation}", operationName);
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "upstream_timeout" });
            default:
                _logger.LogError(ex, "Auth gRPC failed during {Operation} with status {StatusCode}", operationName, ex.StatusCode);
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "upstream_error" });
        }
    }
}
