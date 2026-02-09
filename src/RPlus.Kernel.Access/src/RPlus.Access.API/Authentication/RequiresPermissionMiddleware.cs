using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Application.Services;
using RPlus.SDK.Access.Authorization;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace RPlus.Access.Api.Authentication;

public sealed class RequiresPermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequiresPermissionMiddleware> _logger;

    public RequiresPermissionMiddleware(RequestDelegate next, ILogger<RequiresPermissionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IEffectiveRightsService effectiveRightsService, IRootAccessService rootAccessService)
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
        if (string.IsNullOrWhiteSpace(userIdRaw) || !Guid.TryParse(userIdRaw, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        if (await rootAccessService.IsRootAsync(userIdRaw, context.RequestAborted))
        {
            await _next(context);
            return;
        }

        var tenantId = ResolveTenantId(context.User);
        string json;
        try
        {
            json = await effectiveRightsService.GetEffectivePermissionsJsonAsync(userId, tenantId, context: null, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve effective rights for {UserId}", userId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "access_unavailable" });
            return;
        }

        Dictionary<string, bool>? permissions;
        try
        {
            permissions = string.IsNullOrWhiteSpace(json)
                ? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                : JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
        }
        catch (JsonException)
        {
            permissions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        permissions ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var req in required)
        {
            var permissionId = (req.PermissionId ?? string.Empty).Trim();
            if (permissionId.Length == 0)
                continue;

            if (!permissions.TryGetValue(permissionId, out var allowed) || !allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "forbidden", permission = permissionId });
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
