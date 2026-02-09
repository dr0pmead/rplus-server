using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RPlusGrpc.Guard;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Controllers;

using GrpcStatusCode = Grpc.Core.StatusCode;

[ApiController]
// Canonical route: /api/{service}/{method}
[Route("api/pow")]
// Legacy route kept for backward compatibility
[Route("api/guard/pow")]
[EnableRateLimiting("public")]
public sealed class GuardGatewayController : ControllerBase
{
    private readonly GuardService.GuardServiceClient _guardClient;
    private readonly ILogger<GuardGatewayController> _logger;

    public GuardGatewayController(
        GuardService.GuardServiceClient guardClient,
        ILogger<GuardGatewayController> logger)
    {
        _guardClient = guardClient;
        _logger = logger;
    }

    [HttpPost("challenge")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateChallenge(
        [FromBody] CreateChallengeRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            request.IpAddress = GetClientIpAddress();

            var response = await _guardClient.CreateChallengeAsync(request, cancellationToken: ct);
            return Ok(new
            {
                challengeId = response.ChallengeId,
                salt = response.Salt,
                difficulty = response.Difficulty,
                expiresAt = response.ExpiresAt,
                scope = response.Scope
            });
        }
        catch (RpcException ex)
        {
            return HandleGuardRpcException(ex, "CreateChallenge");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy CreateChallenge to Guard gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] VerifyPowRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            if (string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Nonce))
                return BadRequest(new { error = "invalid_request" });

            request.IpAddress = GetClientIpAddress();

            var response = await _guardClient.VerifyPowAsync(request, cancellationToken: ct);
            return response.IsValid ? Ok(new
            {
                isValid = true,
                hash = response.Hash
            }) : Ok(new
            {
                isValid = false,
                error = response.Error,
                hash = response.Hash
            });
        }
        catch (RpcException ex)
        {
            return HandleGuardRpcException(ex, "VerifyPow");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy Verify to Guard gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    private string GetClientIpAddress()
    {
        // We deliberately do NOT trust client-supplied ip_address in the request body.
        // If the gateway is behind a reverse-proxy, configure Forwarded Headers middleware
        // (with KnownNetworks/KnownProxies) so RemoteIpAddress resolves to the real client IP.
        return HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private IActionResult HandleGuardRpcException(RpcException ex, string operationName)
    {
        // Translate upstream gRPC failures into safe, meaningful HTTP responses.
        // Avoid returning upstream status details to callers (can leak internals).
        switch (ex.StatusCode)
        {
            case GrpcStatusCode.InvalidArgument:
                _logger.LogInformation(ex, "Guard gRPC rejected request (InvalidArgument) during {Operation}", operationName);
                return BadRequest(new { error = "invalid_request" });
            case GrpcStatusCode.Unauthenticated:
                _logger.LogInformation(ex, "Guard gRPC rejected request (Unauthenticated) during {Operation}", operationName);
                return Unauthorized(new { error = "unauthorized" });
            case GrpcStatusCode.PermissionDenied:
                _logger.LogInformation(ex, "Guard gRPC rejected request (PermissionDenied) during {Operation}", operationName);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
            case GrpcStatusCode.NotFound:
                _logger.LogInformation(ex, "Guard gRPC returned NotFound during {Operation}", operationName);
                return NotFound(new { error = "not_found" });
            case GrpcStatusCode.Unavailable:
                _logger.LogWarning(ex, "Guard gRPC unavailable during {Operation}", operationName);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });
            case GrpcStatusCode.DeadlineExceeded:
                _logger.LogWarning(ex, "Guard gRPC deadline exceeded during {Operation}", operationName);
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "upstream_timeout" });
            default:
                _logger.LogError(ex, "Guard gRPC failed during {Operation} with status {StatusCode}", operationName, ex.StatusCode);
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "upstream_error" });
        }
    }
}
