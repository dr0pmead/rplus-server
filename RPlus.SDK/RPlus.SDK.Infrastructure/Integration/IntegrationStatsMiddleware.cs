using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Core.Errors;

namespace RPlus.SDK.Infrastructure.Integration;

public sealed class IntegrationStatsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IIntegrationStatsPublisher _publisher;

    public IntegrationStatsMiddleware(RequestDelegate next, IIntegrationStatsPublisher publisher)
    {
        _next = next;
        _publisher = publisher;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<ExternalAttribute>();
        if (metadata?.Scope is null)
        {
            return;
        }

        if (!TryGetGuidClaim(context.User, "partner_id", out var partnerId) ||
            !TryGetGuidClaim(context.User, "key_id", out var keyId))
        {
            return;
        }

        var environment = context.User.FindFirst("env")?.Value ?? "unknown";
        var correlationId = ResolveCorrelationId(context);
        var route = $"{context.Request.Method} {context.Request.Path}";
        var statusCode = context.Response.StatusCode;

        var statsEvent = new IntegrationStatsEvent(
            partnerId,
            keyId,
            environment,
            metadata.Scope,
            route,
            statusCode,
            stopwatch.ElapsedMilliseconds,
            correlationId,
            MapErrorCode(statusCode));

        try
        {
            await _publisher.PublishAsync(statsEvent, context.RequestAborted);
        }
        catch
        {
            // instrumentation failures should not affect request processing
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var fromRequest) &&
            !StringValues.IsNullOrEmpty(fromRequest))
        {
            return fromRequest.ToString();
        }

        if (context.Response.Headers.TryGetValue("X-Correlation-Id", out var fromResponse) &&
            !StringValues.IsNullOrEmpty(fromResponse))
        {
            return fromResponse.ToString();
        }

        return context.Items.TryGetValue("X-Correlation-Id", out var stored)
            ? stored?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryGetGuidClaim(ClaimsPrincipal principal, string type, out Guid value)
    {
        return Guid.TryParse(principal.FindFirst(type)?.Value, out value);
    }

    private static ErrorCategory MapErrorCode(int statusCode)
    {
        if (statusCode >= 500)
        {
            return ErrorCategory.DownstreamUnavailable;
        }

        return statusCode switch
        {
            401 => ErrorCategory.Unauthorized,
            403 => ErrorCategory.Forbidden,
            404 => ErrorCategory.NotFound,
            409 => ErrorCategory.Conflict,
            429 => ErrorCategory.RateLimitExceeded,
            _ => ErrorCategory.None
        };
    }
}
