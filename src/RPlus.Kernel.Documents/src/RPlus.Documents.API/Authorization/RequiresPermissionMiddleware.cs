using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using RPlus.Documents.Api.Authorization;
using RPlus.SDK.Access.Authorization;
using RPlusGrpc.Access;
using System.Security.Claims;

namespace RPlus.Documents.Api.Authorization;

public sealed class RequiresPermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequiresPermissionMiddleware> _logger;

    public RequiresPermissionMiddleware(RequestDelegate next, ILogger<RequiresPermissionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AccessService.AccessServiceClient accessClient)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var required = endpoint.Metadata.GetOrderedMetadata<RequiresPermissionAttribute>();
        var anyRequired = endpoint.Metadata.GetOrderedMetadata<RequiresAnyPermissionAttribute>();
        if (required == null || required.Count == 0)
        {
            required = new List<RequiresPermissionAttribute>();
        }

        if (IsSelfAllowed(context, endpoint))
        {
            await _next(context);
            return;
        }

        // Internal service-to-service calls (service secret) bypass permission checks.
        // The request is already protected by X-RPlus-Internal middleware.
        if (string.Equals(context.User.FindFirstValue("auth_type"), "service_secret", StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        var userIdRaw = GetUserId(context.User);
        if (string.IsNullOrWhiteSpace(userIdRaw))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        var tenantId = ResolveTenantId(context.User);

        foreach (var req in required)
        {
            var permissionId = (req.PermissionId ?? string.Empty).Trim();
            if (permissionId.Length == 0)
                continue;

            try
            {
                var response = await accessClient.CheckPermissionAsync(new CheckPermissionRequest
                {
                    UserId = userIdRaw,
                    TenantId = tenantId.ToString(),
                    PermissionId = permissionId,
                    ApplicationId = "documents"
                }, cancellationToken: context.RequestAborted);

                if (!response.IsAllowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "forbidden", permission = permissionId, reason = response.Reason });
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check permission {PermissionId} for user {UserId}", permissionId, userIdRaw);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = "access_unavailable" });
                return;
            }
        }

        if (anyRequired != null && anyRequired.Count > 0)
        {
            var allowed = false;
            foreach (var req in anyRequired)
            {
                foreach (var permissionId in req.PermissionIds)
                {
                    var perm = (permissionId ?? string.Empty).Trim();
                    if (perm.Length == 0)
                        continue;

                    try
                    {
                        var response = await accessClient.CheckPermissionAsync(new CheckPermissionRequest
                        {
                            UserId = userIdRaw,
                            TenantId = tenantId.ToString(),
                            PermissionId = perm,
                            ApplicationId = "documents"
                        }, cancellationToken: context.RequestAborted);

                        if (response.IsAllowed)
                        {
                            allowed = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check permission {PermissionId} for user {UserId}", perm, userIdRaw);
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        await context.Response.WriteAsJsonAsync(new { error = "access_unavailable" });
                        return;
                    }
                }

                if (allowed)
                    break;
            }

            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "forbidden" });
                return;
            }
        }

        await _next(context);
    }

    private static bool IsSelfAllowed(HttpContext context, Endpoint endpoint)
    {
        var allowSelf = endpoint.Metadata.GetMetadata<AllowSelfAttribute>();
        if (allowSelf == null)
            return false;

        var paramName = string.IsNullOrWhiteSpace(allowSelf.RouteUserIdParameterName) ? "userId" : allowSelf.RouteUserIdParameterName;
        if (!context.Request.RouteValues.TryGetValue(paramName, out var routeValue) || routeValue == null)
            return false;

        var routeText = routeValue.ToString();
        if (string.IsNullOrWhiteSpace(routeText) || !Guid.TryParse(routeText, out var routeUserId) || routeUserId == Guid.Empty)
            return false;

        var userIdRaw = GetUserId(context.User);
        if (!Guid.TryParse(userIdRaw, out var currentUserId) || currentUserId == Guid.Empty)
            return false;

        return routeUserId == currentUserId;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub")
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

    private static Guid ResolveTenantId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue("tenant_id") ?? principal.FindFirstValue("tenantId");
        return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
