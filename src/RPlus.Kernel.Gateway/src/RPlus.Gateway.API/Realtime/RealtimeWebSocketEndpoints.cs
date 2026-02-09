using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RPlus.SDK.Gateway.Realtime;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public static class RealtimeWebSocketEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapRealtimeWebSocketEndpoint(this WebApplication app)
    {
        app.MapGet("/realtime", async (HttpContext context, CancellationToken ct) =>
        {
            if (context.Request.Headers.ContainsKey("Authorization"))
                return Results.BadRequest(new { error = "forbidden_auth_channel" });

            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest(new { error = "websocket_required" });

            var userId = context.User.FindFirst("sub")?.Value
                         ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            var policy = context.RequestServices.GetRequiredService<IRealtimePolicyService>();
            var registry = await policy.GetRegistryAsync(userId, ct);
            await SendAsync(socket, RealtimeSystemEventsMessage.Create(registry), ct);

            var hub = context.RequestServices.GetRequiredService<IRealtimeEventHub>();
            using var subscription = hub.Subscribe(userId, ct);

            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(15));
            var receiveTask = ReceiveLoopAsync(socket, ct);
            var sendTask = SendLoopAsync(socket, subscription, heartbeat, ct);

            await Task.WhenAny(receiveTask, sendTask);

            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", ct);
            }
            catch
            {
                // ignore
            }

            return Results.Empty;
        }).RequireAuthorization("Default");
    }

    private static async Task SendLoopAsync(
        WebSocket socket,
        RealtimeSubscription subscription,
        PeriodicTimer heartbeat,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var readTask = subscription.Reader.ReadAsync(ct).AsTask();
            var tickTask = heartbeat.WaitForNextTickAsync(ct).AsTask();

            var completed = await Task.WhenAny(readTask, tickTask);

            if (completed == tickTask && tickTask.Result)
            {
                await SendAsync(socket, new { type = "system.ping" }, ct);
                continue;
            }

            if (completed == readTask)
            {
                var message = await readTask;
                await SendAsync(socket, message, ct);
            }
        }
    }

    private static async Task ReceiveLoopAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, ct);
            }
            catch
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }

    private static Task SendAsync(WebSocket socket, object message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct);
    }
}

