using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System;

#nullable enable
namespace RPlus.Gateway.Api.Middleware;

public class HeaderSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] InternalHeaders = { "X-RPlus-Internal", "X-RPlus-Admin" };

    public HeaderSanitizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var allowInternal = context.Request.Path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase);
        foreach (var header in InternalHeaders)
        {
            if (allowInternal && header.Equals("X-RPlus-Internal", StringComparison.OrdinalIgnoreCase))
                continue;

            context.Request.Headers.Remove(header);
        }
        await _next(context);
    }
}
