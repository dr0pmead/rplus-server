using Fido2NetLib;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using RPlus.Auth.Application.Commands.Passkey;
using RPlus.SDK.Auth.Commands;
using RPlus.SDK.Auth.Enums;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Application.Security;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

[ApiController]
[Route("v1")]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITokenService _tokenService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IMediator mediator,
        ITokenService tokenService,
        IHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _tokenService = tokenService;
        _environment = environment;
        _logger = logger;
    }


    [HttpPost("login/password")]
    public async Task<ActionResult<LoginWithPasswordResponse>> LoginWithPassword([FromBody] LoginWithPasswordCommand command)
    {
        if (string.IsNullOrEmpty(command.ClientIp))
            command = command with { ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() };
        
        if (string.IsNullOrEmpty(command.UserAgent))
            command = command with { UserAgent = Request.Headers.UserAgent.ToString() };

        var result = await _mediator.Send(command);
        if (result.Success)
            return Ok(result);

        if (result.Error == "rate_limit_exceeded")
        {
            if (result.RetryAfterSeconds.HasValue)
                Response.Headers.RetryAfter = result.RetryAfterSeconds.Value.ToString();
            return StatusCode(429, result);
        }

        return BadRequest(result);
    }

    [HttpPost("identify")]
    public async Task<ActionResult<IdentifyResponse>> Identify([FromBody] IdentifyRequest request)
    {
        var command = new IdentifyCommand(
            request.Identifier, 
            HttpContext.Connection.RemoteIpAddress?.ToString(), 
            Request.Headers.UserAgent.ToString());
            
        var result = await _mediator.Send(command);
        if (!result.Exists) return NotFound(new { error = "user_not_found" });

        if (result.Error == "user_blocked") return StatusCode(403, new { error = result.Error });

        return Ok(result);
    }

    [HttpPost("passkey/register/begin")]
    public async Task<IActionResult> BeginRegisterPasskey([FromBody] GetPasskeyRegistrationOptionsQuery query)
    {
        if (ReservedLogins.IsReserved(query.Username))
            return BadRequest(new { error = "login_reserved", code = "LOGIN_RESERVED" });

        var options = await _mediator.Send(query);
        return Ok(options);
    }

    [HttpPost("passkey/register/complete")]
    public async Task<IActionResult> CompleteRegisterPasskey([FromBody] CompletePasskeyRegistrationCommand command)
    {
        if (ReservedLogins.IsReserved(command.Username))
            return BadRequest(new { error = "login_reserved", code = "LOGIN_RESERVED" });

        var success = await _mediator.Send(command);
        return success ? Ok() : BadRequest("registration_failed");
    }

    [HttpPost("passkey/login/begin")]
    public async Task<IActionResult> BeginLoginPasskey([FromBody] GetPasskeyAssertionOptionsQuery query)
    {
        var options = await _mediator.Send(query);
        return Ok(options);
    }

    [HttpPost("passkey/login/complete")]
    public async Task<IActionResult> CompleteLoginPasskey([FromBody] CompletePasskeyAssertionCommand command)
    {
        command = command with
        {
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result.ErrorCode);

        var tokens = await _tokenService.IssueTokensAsync(
            (AuthUserEntity)result.User!, (DeviceEntity)result.Device!, null, command.ClientIp, command.UserAgent, HttpContext.RequestAborted);

        return Ok(new VerifyResponse(
            tokens.AccessToken, 
            tokens.RefreshToken, 
            tokens.AccessExpiresAt, 
            tokens.RefreshExpiresAt, 
            new UserPayload(result.User!.Id)));
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequest request, CancellationToken ct)
    {
        var command = new RequestOtpCommand(
            request.Phone, 
            ResolveDeviceId(request.DeviceId), 
            HttpContext.Connection.RemoteIpAddress?.ToString(), 
            Request.Headers.UserAgent.ToString(), 
            request.Channel);
            
        var result = await _mediator.Send(command, ct);
        if (!result.Success)
        {
            if (result.ErrorCode == "user_blocked") return StatusCode(403, new { error = result.ErrorCode });
            if (result.ErrorCode == "rate_limit_exceeded" || result.ErrorCode == "too_soon")
            {
                return StatusCode(429, new { error = result.ErrorCode, retryAfterSeconds = result.RetryAfterSeconds });
            }
            return BadRequest(new { error = result.ErrorCode, retryAfterSeconds = result.RetryAfterSeconds });
        }

        var debugCode = _environment.IsDevelopment() ? result.Code : null;
        return Ok(new OtpRequestResponse(true, result.RetryAfterSeconds, debugCode, null, result.UserExists, result.SelectedChannel));
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequest request, CancellationToken ct)
    {
        var deviceId = ResolveDeviceId(request.DeviceId);
        var command = new VerifyOtpCommand(
            request.Phone, request.Code, deviceId, request.DevicePublicKey, 
            HttpContext.Connection.RemoteIpAddress?.ToString(), 
            Request.Headers.UserAgent.ToString());

        var result = await _mediator.Send(command, ct);
        if (result.Status != OtpVerifyStatus.Success)
        {
            return result.Status switch
            {
                OtpVerifyStatus.Expired => BadRequest(new { error = "otp_expired" }),
                OtpVerifyStatus.InvalidCode => BadRequest(new { error = "invalid_code" }),
                OtpVerifyStatus.AttemptsExceeded => StatusCode(429, new { error = "otp_blocked" }),
                OtpVerifyStatus.UserBlocked => StatusCode(403, new { error = "user_blocked" }),
                _ => BadRequest(new { error = "verification_failed" })
            };
        }

        var tokens = await _tokenService.IssueTokensAsync(
            (AuthUserEntity)result.User!, (DeviceEntity)result.Device!, request.DevicePublicKey, 
            HttpContext.Connection.RemoteIpAddress?.ToString(), 
            Request.Headers.UserAgent.ToString(), ct);

        return Ok(new VerifyResponse(
            tokens.AccessToken, tokens.RefreshToken, tokens.AccessExpiresAt, tokens.RefreshExpiresAt, 
            new UserPayload(result.User!.Id)));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var deviceId = ResolveDeviceId(request.DeviceId);
        var command = new RefreshTokenCommand(
            request.RefreshToken, deviceId, request.DevicePublicKey, 
            HttpContext.Connection.RemoteIpAddress?.ToString(), 
            Request.Headers.UserAgent.ToString());

        var result = await _mediator.Send(command, ct);
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "token_compromised" => Unauthorized(new { error = "token_theft_detected", message = "Security incident occurred." }),
                "user_blocked" => StatusCode(403, new { error = "user_blocked" }),
                _ => Unauthorized(new { error = result.ErrorCode ?? "refresh_failed" })
            };
        }

        var tokens = await _tokenService.IssueTokensAsync(
            (AuthUserEntity)result.User!, (DeviceEntity)result.Device!, request.DevicePublicKey, 
            HttpContext.Connection.RemoteIpAddress?.ToString(), 
            Request.Headers.UserAgent.ToString(), ct);

        return Ok(new RefreshResponse(tokens.AccessToken, tokens.RefreshToken, tokens.AccessExpiresAt, tokens.RefreshExpiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await _tokenService.RevokeAsync(request.RefreshToken, ResolveDeviceId(request.DeviceId), ct);
        return NoContent();
    }

    private string ResolveDeviceId(string deviceIdFromBody)
    {
        if (Request.Headers.TryGetValue("X-Device-Id", out var headerId) && !string.IsNullOrWhiteSpace(headerId))
            return headerId.ToString();
        return deviceIdFromBody;
    }

    public record IdentifyRequest(string Identifier);
    public record OtpRequest(string Phone, string DeviceId, string? Channel = null);
    public record OtpVerifyRequest(string Phone, string Code, string DeviceId, string? DevicePublicKey);
    public record RefreshRequest(string RefreshToken, string DeviceId, string? DevicePublicKey);
    public record LogoutRequest(string RefreshToken, string DeviceId);
    public record OtpRequestResponse(bool Sent, int RetryAfterSeconds, string? DebugCode, string? ErrorCode, bool AccountExists, string? Channel);
    public record VerifyResponse(string AccessToken, string RefreshToken, DateTime AccessExpiresAt, DateTime RefreshExpiresAt, UserPayload User);
    public record RefreshResponse(string AccessToken, string RefreshToken, DateTime AccessExpiresAt, DateTime RefreshExpiresAt);
    public record UserPayload(Guid Id);
}
