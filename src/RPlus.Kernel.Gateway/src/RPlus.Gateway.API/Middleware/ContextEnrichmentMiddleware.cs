using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Middleware;

public class ContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public ContextEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = context.User.FindFirst("tenant_id")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                context.Items["UserId"] = userId;
            }
            if (!string.IsNullOrEmpty(tenantId))
            {
                context.Items["TenantId"] = tenantId;
            }
        }

        await _next(context);
    }
}
