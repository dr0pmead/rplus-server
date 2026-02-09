using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPlus.Users.Application.Interfaces.Services;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using RPlusGrpc.Access;
using System.Net.Http;

namespace RPlus.Users.Api.Controllers;

[ApiController]
[Route("api/users/qr")]
public sealed class UserQrController : ControllerBase
{
    private readonly IUserQrService _qrService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserQrController> _logger;

    public UserQrController(IUserQrService qrService, IConfiguration configuration, ILogger<UserQrController> logger)
    {
        _qrService = qrService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("issue")]
    [Authorize]
    public async Task<IActionResult> Issue(CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
            return Unauthorized();

        var result = await _qrService.IssueAsync(userGuid, HttpContext.TraceIdentifier, ct);

        return Ok(new
        {
            token = result.Token,
            expiresAt = result.ExpiresAt.ToUniversalTime().ToString("O"),
            ttlSeconds = result.TtlSeconds
        });
    }

    [HttpPost("issue/{userId}")]
    [Authorize]
    public async Task<IActionResult> IssueForUser(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var targetUserId))
            return BadRequest(new { error = "invalid_user_id" });

        var currentUserId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(currentUserId) || !Guid.TryParse(currentUserId, out var currentUserGuid))
            return Unauthorized();

        var isSelf = string.Equals(currentUserId, targetUserId.ToString(), StringComparison.OrdinalIgnoreCase);

        if (!isSelf)
        {
            var allowed = await CheckAccessPermissionAsync(currentUserGuid, "users.manage", ct);
            if (!allowed)
                return Forbid();
        }

        var result = await _qrService.IssueAsync(targetUserId, HttpContext.TraceIdentifier, ct);

        return Ok(new
        {
            token = result.Token,
            expiresAt = result.ExpiresAt.ToUniversalTime().ToString("O"),
            ttlSeconds = result.TtlSeconds
        });
    }

    private async Task<bool> CheckAccessPermissionAsync(Guid userId, string permissionId, CancellationToken ct)
    {
        var accessGrpcAddress =
            _configuration["Services:Access:Grpc"]
            ?? $"http://{_configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{_configuration["ACCESS_GRPC_PORT"] ?? "5003"}";

        var tenantId = ResolveTenantId(HttpContext);

        try
        {
            using var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                UseCookies = false
            };
            using var httpClient = new HttpClient(handler);
            using var channel = GrpcChannel.ForAddress(accessGrpcAddress, new GrpcChannelOptions
            {
                HttpClient = httpClient
            });

            var client = new AccessService.AccessServiceClient(channel);
            var response = await client.CheckPermissionAsync(new CheckPermissionRequest
            {
                UserId = userId.ToString("D"),
                TenantId = tenantId.ToString("D"),
                PermissionId = permissionId,
                ApplicationId = "users"
            }, cancellationToken: ct);

            return response.IsAllowed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access permission check failed for {UserId} {PermissionId}", userId, permissionId);
            return false;
        }
    }

    private static Guid ResolveTenantId(HttpContext context)
    {
        var claim = context.User.FindFirstValue("tenant_id") ?? context.User.FindFirstValue("tenantId");
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var tenantFromClaim))
            return tenantFromClaim;

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var header) &&
            Guid.TryParse(header.ToString(), out var tenantFromHeader))
            return tenantFromHeader;

        return Guid.Empty;
    }
}
