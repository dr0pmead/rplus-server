using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Access.Authorization;
using RPlusGrpc.Access;
using System.Security.Claims;

namespace RPlus.Meta.Api.Authorization;

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
        if (required == null || required.Count == 0)
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
                    ApplicationId = "meta"
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

        await _next(context);
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
