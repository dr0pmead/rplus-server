using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Middleware;

public class TraceIdMiddleware
{
    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.Request.Headers["X-Trace-Id"].ToString();
        if (string.IsNullOrEmpty(traceId))
        {
            traceId = Guid.NewGuid().ToString();
            context.Request.Headers["X-Trace-Id"] = traceId;
        }

        context.Response.Headers["X-Trace-Id"] = traceId;
        await _next(context);
    }
}
