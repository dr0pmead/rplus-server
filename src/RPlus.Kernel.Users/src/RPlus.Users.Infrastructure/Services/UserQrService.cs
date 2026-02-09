using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.SDK.Eventing;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.Users.Application.Interfaces.Services;
using RPlus.Users.Application.Options;
using RPlus.Users.Infrastructure.Events;
using StackExchange.Redis;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Users.Infrastructure.Services;

public sealed class UserQrService : IUserQrService
{
    private const string ActiveKeyPrefix = "users:qr:active:";
    private const string TokenKeyPrefix = "users:qr:token:";
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new();

    private readonly IConnectionMultiplexer _redis;
    private readonly IOptionsMonitor<UserQrOptions> _options;
    private readonly IEventPublisher _events;
    private readonly ILogger<UserQrService> _logger;

    public UserQrService(
        IConnectionMultiplexer redis,
        IOptionsMonitor<UserQrOptions> options,
        IEventPublisher events,
        ILogger<UserQrService> logger)
    {
        _redis = redis;
        _options = options;
        _events = events;
        _logger = logger;
    }

    public async Task<UserQrIssueResult> IssueAsync(Guid userId, string? traceId, CancellationToken ct)
    {
        var settings = _options.CurrentValue;
        var tokenBytes = Math.Clamp(settings.TokenBytes, 12, 32);
        var ttlSeconds = Math.Clamp(settings.TtlSeconds, 10, 300);
        var ttl = TimeSpan.FromSeconds(ttlSeconds);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        var db = _redis.GetDatabase();
        var activeKey = ActiveKeyPrefix + userId.ToString("D");

        try
        {
            var oldToken = await db.StringGetAsync(activeKey);
            if (!oldToken.IsNullOrEmpty)
            {
                await db.KeyDeleteAsync(TokenKeyPrefix + oldToken.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear previous QR token for {UserId}", userId);
        }

        var token = GenerateToken(tokenBytes);
        var tokenKey = TokenKeyPrefix + token;

        await db.StringSetAsync(activeKey, token, ttl);
        await db.StringSetAsync(tokenKey, userId.ToString("D"), ttl);

        await PublishIssuedEventAsync(userId, token, expiresAt, traceId, ct);

        return new UserQrIssueResult(token, expiresAt, ttlSeconds);
    }

    private async Task PublishIssuedEventAsync(
        Guid userId,
        string token,
        DateTimeOffset expiresAt,
        string? traceId,
        CancellationToken ct)
    {
        try
        {
            var payload = new UserQrIssuedEvent(userId, token, expiresAt);
            var trace = Guid.TryParse(traceId, out var parsed) ? parsed : Guid.NewGuid();
            var envelope = new EventEnvelope<UserQrIssuedEvent>(
                payload,
                source: "rplus.users",
                eventType: UserQrIssuedEvent.EventName,
                aggregateId: userId.ToString("D"),
                traceId: trace);

            var json = JsonSerializer.Serialize(envelope, EnvelopeJsonOptions);
            await _events.PublishRawAsync(UserQrIssuedEvent.EventName, json, userId.ToString("D"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish {EventType} for {UserId}", UserQrIssuedEvent.EventName, userId);
        }
    }

    private static string GenerateToken(int bytes)
    {
        Span<byte> buffer = stackalloc byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
