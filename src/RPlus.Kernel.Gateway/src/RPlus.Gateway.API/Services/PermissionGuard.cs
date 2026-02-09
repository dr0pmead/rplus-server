using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlusGrpc.Access;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Gateway.Api.Services;

public sealed class PermissionGuard
{
    private readonly AccessService.AccessServiceClient _access;
    private readonly ProxyAuthorizationOptions _options;
    private readonly ILogger<PermissionGuard> _logger;

    public PermissionGuard(
        AccessService.AccessServiceClient access,
        IOptions<ProxyAuthorizationOptions> options,
        ILogger<PermissionGuard> logger)
    {
        _access = access;
        _options = options.Value ?? new ProxyAuthorizationOptions();
        _logger = logger;
    }

    public async Task<(bool Allowed, string? Error)> CheckAsync(
        HttpContext context,
        string permissionId,
        CancellationToken ct)
    {
        if (!_options.Enabled)
            return (true, null);

        var userId = GetUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "unauthorized");

        var tenantId = ResolveTenantId(context);
        var applicationId = ResolveApplicationId(context);

        try
        {
            var response = await _access.CheckPermissionAsync(
                new CheckPermissionRequest
                {
                    UserId = userId,
                    TenantId = tenantId.ToString(),
                    ApplicationId = applicationId,
                    PermissionId = permissionId,
                    Context = BuildContextJson(context)
                },
                cancellationToken: ct);

            return (response.IsAllowed, response.IsAllowed ? null : "forbidden");
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Access gRPC unavailable during permission check for {PermissionId}", permissionId);
            return _options.FailOpenOnAccessUnavailable ? (true, null) : (false, "access_unavailable");
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Access gRPC failed during permission check for {PermissionId} with {Status}", permissionId, ex.StatusCode);
            return _options.FailOpenOnAccessUnavailable ? (true, null) : (false, "access_error");
        }
    }

    private Guid ResolveTenantId(HttpContext context)
    {
        var claim = context.User.FindFirstValue("tenant_id") ?? context.User.FindFirstValue("tenantId");
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var fromClaim))
            return fromClaim;

        if (context.Request.Headers.TryGetValue(_options.TenantIdHeaderName, out var header) &&
            Guid.TryParse(header.ToString(), out var fromHeader))
        {
            return fromHeader;
        }

        return Guid.Empty;
    }

    private string ResolveApplicationId(HttpContext context)
    {
        var appId = _options.DefaultApplicationId;

        if (context.Request.Headers.TryGetValue(_options.ApplicationIdHeaderName, out var headerValue))
        {
            var raw = headerValue.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
                appId = raw;
        }

        return appId;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub")
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

    private static string BuildContextJson(HttpContext context)
    {
        var payload = new System.Collections.Generic.Dictionary<string, string?>
        {
            ["ip"] = context.Connection.RemoteIpAddress?.ToString(),
            ["method"] = context.Request.Method,
            ["path"] = context.Request.Path.Value,
            ["userAgent"] = context.Request.Headers.UserAgent.ToString()
        };

        return JsonSerializer.Serialize(payload);
    }
}

