using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RPlus.SDK.Gateway.Realtime;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public static class RealtimeEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapRealtimeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/realtime/stream", async (HttpContext context, CancellationToken ct) =>
        {
            if (context.Request.Headers.ContainsKey("Authorization"))
                return Results.BadRequest(new { error = "forbidden_auth_channel" });

            var options = context.RequestServices.GetRequiredService<IOptionsMonitor<RealtimeGatewayOptions>>().CurrentValue;

            var userId = context.User.FindFirst("sub")?.Value
                         ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            context.Response.ContentType = "text/event-stream";

            var hub = context.RequestServices.GetRequiredService<IRealtimeEventHub>();
            using var subscription = hub.Subscribe(userId, ct);

            // Initial ping (helps proxies open stream)
            await context.Response.WriteAsync(": ok\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            // Registry handshake (transport-agnostic message over SSE)
            var policy = context.RequestServices.GetRequiredService<IRealtimePolicyService>();
            var registry = await policy.GetRegistryAsync(userId, ct);
            var systemEvents = RealtimeSystemEventsMessage.Create(registry);
            await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(systemEvents, JsonOptions)}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(15));
            var readTask = ReadStreamAsync(context, subscription, ct);

            while (!ct.IsCancellationRequested && !readTask.IsCompleted)
            {
                if (await heartbeat.WaitForNextTickAsync(ct))
                {
                    await context.Response.WriteAsync(": ping\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
            }

            await readTask;
            return Results.Empty;
        }).RequireAuthorization("Default");
    }

    private static async Task ReadStreamAsync(HttpContext context, RealtimeSubscription subscription, CancellationToken ct)
    {
        await foreach (var message in subscription.Reader.ReadAllAsync(ct))
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }
}
