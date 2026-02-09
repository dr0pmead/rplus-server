using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using StackExchange.Redis;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Infrastructure.Services;

public sealed class SystemAuthFlowStore : ISystemAuthFlowStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SystemAuthFlowStore> _logger;

    public SystemAuthFlowStore(IConnectionMultiplexer redis, ILogger<SystemAuthFlowStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> CreateAsync(SystemAuthFlow flow, TimeSpan ttl, CancellationToken ct)
    {
        var token = GenerateToken();
        var db = _redis.GetDatabase();
        var key = BuildKey(token);

        var payload = JsonSerializer.Serialize(flow, JsonOptions);
        var ok = await db.StringSetAsync(key, payload, ttl);
        if (!ok)
        {
            _logger.LogWarning("Failed to store system auth flow in Redis.");
        }

        return token;
    }

    public async Task<SystemAuthFlow?> GetAsync(string tempToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tempToken))
            return null;

        var db = _redis.GetDatabase();
        var key = BuildKey(tempToken);
        var value = await db.StringGetAsync(key);
        if (!value.HasValue)
            return null;

        try
        {
            return JsonSerializer.Deserialize<SystemAuthFlow>(value.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize system auth flow.");
            return null;
        }
    }

    public async Task DeleteAsync(string tempToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tempToken))
            return;

        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(tempToken));
    }

    private static string BuildKey(string token) => $"auth:sysadmin:flow:{token}";

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
