using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using StackExchange.Redis;

namespace RPlus.Auth.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of short code service for partner scan fallback.
/// Codes are 6-digit numeric strings stored with 120s TTL.
/// </summary>
public sealed class ShortCodeService : IShortCodeService
{
    private const string KeyPrefix = "auth:otp:";
    private const int CodeLength = 6;
    private const int TtlSeconds = 120;
    private const int MaxRetries = 3;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ShortCodeService> _logger;

    public ShortCodeService(
        IConnectionMultiplexer redis,
        ILogger<ShortCodeService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<ShortCodeGenerateResult> GenerateAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            var code = GenerateRandomCode();
            var key = $"{KeyPrefix}{code}";

            // SETNX with TTL - atomic "set if not exists"
            var wasSet = await db.StringSetAsync(
                key,
                userId.ToString(),
                TimeSpan.FromSeconds(TtlSeconds),
                When.NotExists
            ).ConfigureAwait(false);

            if (wasSet)
            {
                _logger.LogInformation(
                    "Generated short code for user {UserId}, expires in {Ttl}s",
                    userId, TtlSeconds);

                return ShortCodeGenerateResult.Ok(code);
            }

            _logger.LogDebug(
                "Short code collision on attempt {Attempt}, retrying...",
                attempt + 1);
        }

        _logger.LogWarning(
            "Failed to generate unique short code after {MaxRetries} attempts for user {UserId}",
            MaxRetries, userId);

        return ShortCodeGenerateResult.Fail("code_generation_failed");
    }

    /// <summary>
    /// Generates a cryptographically secure 6-digit code.
    /// </summary>
    private static string GenerateRandomCode()
    {
        // Generate random number 0-999999
        var bytes = RandomNumberGenerator.GetBytes(4);
        var value = BitConverter.ToUInt32(bytes) % 1_000_000;
        return value.ToString().PadLeft(CodeLength, '0');
    }
}
