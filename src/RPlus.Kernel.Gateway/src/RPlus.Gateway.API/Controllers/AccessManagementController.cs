using Grpc.Core;
using GrpcStatusCode = Grpc.Core.StatusCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RPlus.Gateway.Api.Services;
using RPlusGrpc.Access;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Controllers;

[ApiController]
[Route("api/access")]
[Authorize]
public class AccessManagementController : ControllerBase
{
    private readonly AccessService.AccessServiceClient _client;
    private readonly PermissionGuard _permissionGuard;
    private readonly ILogger<AccessManagementController> _logger;

    public AccessManagementController(
        AccessService.AccessServiceClient client,
        PermissionGuard permissionGuard,
        ILogger<AccessManagementController> logger)
    {
        _client = client;
        _permissionGuard = permissionGuard;
        _logger = logger;
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(
        [FromQuery] string? q,
        [FromQuery] string? appId,
        [FromQuery] bool includeDeprecated,
        CancellationToken cancellationToken)
    {
        var authz = await RequirePermissionAsync("access.roles.read", cancellationToken);
        if (authz != null)
            return authz;

        try
        {
            var response = await _client.GetPermissionsAsync(new Empty(), cancellationToken: cancellationToken);

            var term = (q ?? string.Empty).Trim();
            var termLower = term.Length > 0 ? term.ToLowerInvariant() : string.Empty;

            var appIdNormalized = (appId ?? string.Empty).Trim();
            var appGuid = Guid.Empty;
            var filterByAppId = appIdNormalized.Length > 0 &&
                                Guid.TryParse(appIdNormalized, out appGuid) &&
                                appGuid != Guid.Empty;

            var items = response.Permissions
                .Where(p => includeDeprecated || p.IsActive)
                .Where(p => !filterByAppId || string.Equals(p.ApplicationId, appGuid.ToString(), StringComparison.OrdinalIgnoreCase))
                .Where(p =>
                {
                    if (termLower.Length == 0) return true;
                    var id = p.PermissionId ?? string.Empty;
                    return id.ToLowerInvariant().Contains(termLower);
                })
                .OrderBy(p => p.PermissionId, StringComparer.Ordinal)
                .Select(p =>
                {
                    var id = p.PermissionId ?? string.Empty;
                    SplitPermissionId(id, out var resource, out var action);
                    return new
                    {
                        id,
                        appId = p.ApplicationId ?? string.Empty,
                        resource,
                        action,
                        title = id,
                        description = string.Empty,
                        status = p.IsActive ? "ACTIVE" : "DEPRECATED",
                        supportedContexts = Array.Empty<string>(),
                        sourceService = (string?)null
                    };
                })
                .ToList();

            return Ok(new { items });
        }
        catch (RpcException ex)
        {
            return HandleAccessRpcException(ex, "GetPermissions");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy GetPermissions to Access gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpPost("{permissionId}/activate")]
    public async Task<IActionResult> ActivatePermission(string permissionId, [FromQuery] string applicationId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(permissionId) || string.IsNullOrWhiteSpace(applicationId))
                return BadRequest(new { error = "invalid_request" });

            var request = new ActivatePermissionRequest
            {
                PermissionId = permissionId,
                ApplicationId = applicationId
            };
            await _client.ActivatePermissionAsync(request, cancellationToken: cancellationToken);
            return Ok();
        }
        catch (RpcException ex)
        {
            return HandleAccessRpcException(ex, "ActivatePermission");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy ActivatePermission to Access gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    [HttpGet("effective-rights")]
    public async Task<IActionResult> GetEffectiveRights([FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var resolvedTenantId = string.IsNullOrWhiteSpace(tenantId) ? Guid.Empty.ToString() : tenantId;
            if (!Guid.TryParse(resolvedTenantId, out _))
                return BadRequest(new { error = "invalid_tenant_id" });

            var request = new GetEffectiveRightsRequest
            {
                UserId = userId,
                TenantId = resolvedTenantId
            };

            var response = await _client.GetEffectiveRightsAsync(request, cancellationToken: cancellationToken);

            Dictionary<string, bool>? permissions = null;
            try
            {
                permissions = string.IsNullOrWhiteSpace(response.PermissionsJson)
                    ? new Dictionary<string, bool>()
                    : JsonSerializer.Deserialize<Dictionary<string, bool>>(response.PermissionsJson);
            }
            catch (JsonException)
            {
                permissions = new Dictionary<string, bool>();
            }

            permissions ??= new Dictionary<string, bool>();

            return Ok(new
            {
                userId = userId,
                tenantId = resolvedTenantId,
                permissions = permissions,
                version = response.Version
            });
        }
        catch (RpcException ex)
        {
            return HandleAccessRpcException(ex, "GetEffectiveRights");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy GetEffectiveRights to Access gRPC");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    private async Task<IActionResult?> RequirePermissionAsync(string permissionId, CancellationToken ct)
    {
        var (allowed, error) = await _permissionGuard.CheckAsync(HttpContext, permissionId, ct);
        if (allowed) return null;

        var status = error switch
        {
            "unauthorized" => 401,
            "access_unavailable" => 503,
            "access_error" => 502,
            _ => 403
        };

        return StatusCode(status, new { error = error ?? "forbidden", permission = permissionId });
    }

    private static void SplitPermissionId(string permissionId, out string resource, out string action)
    {
        resource = string.Empty;
        action = string.Empty;

        var id = (permissionId ?? string.Empty).Trim();
        if (id.Length == 0)
            return;

        var lastDot = id.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= id.Length - 1)
        {
            resource = id;
            return;
        }

        resource = id[..lastDot];
        action = id[(lastDot + 1)..];
    }

    private IActionResult HandleAccessRpcException(RpcException ex, string operationName)
    {
        // Translate upstream gRPC failures into safe, meaningful HTTP responses.
        // Avoid returning upstream status details to callers (can leak internals).
        switch (ex.StatusCode)
        {
            case GrpcStatusCode.InvalidArgument:
                _logger.LogInformation(ex, "Access gRPC rejected request (InvalidArgument) during {Operation}", operationName);
                return BadRequest(new { error = "invalid_request" });
            case GrpcStatusCode.Unauthenticated:
                _logger.LogInformation(ex, "Access gRPC rejected request (Unauthenticated) during {Operation}", operationName);
                return Unauthorized(new { error = "unauthorized" });
            case GrpcStatusCode.PermissionDenied:
                _logger.LogInformation(ex, "Access gRPC rejected request (PermissionDenied) during {Operation}", operationName);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
            case GrpcStatusCode.NotFound:
                _logger.LogInformation(ex, "Access gRPC returned NotFound during {Operation}", operationName);
                return NotFound(new { error = "not_found" });
            case GrpcStatusCode.Unavailable:
                _logger.LogWarning(ex, "Access gRPC unavailable during {Operation}", operationName);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });
            case GrpcStatusCode.DeadlineExceeded:
                _logger.LogWarning(ex, "Access gRPC deadline exceeded during {Operation}", operationName);
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "upstream_timeout" });
            default:
                _logger.LogError(ex, "Access gRPC failed during {Operation} with status {StatusCode}", operationName, ex.StatusCode);
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "upstream_error" });
        }
    }
}
