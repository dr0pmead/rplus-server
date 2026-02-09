using System.Text.RegularExpressions;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Eventing.Abstractions;

namespace RPlus.Kernel.Integration.Api.Services;

/// <summary>
/// Result of short code validation.
/// </summary>
public sealed record ShortCodeValidationResult(bool Success, Guid UserId, string? Error)
{
    public static ShortCodeValidationResult Ok(Guid userId) => new(true, userId, null);
    public static ShortCodeValidationResult Fail(string error) => new(false, Guid.Empty, error);
}

/// <summary>
/// Result of short code generation/retrieval.
/// </summary>
public sealed record ShortCodeGenerationResult(bool Success, string? Code, int ExpiresIn, string? Error)
{
    public static ShortCodeGenerationResult Ok(string code, int expiresIn) => new(true, code, expiresIn, null);
    public static ShortCodeGenerationResult Fail(string error) => new(false, null, 0, error);
}

/// <summary>
/// OTP issued event for realtime notifications.
/// </summary>
public sealed record OtpIssuedEvent(Guid UserId, string Code, int ExpiresInSeconds, DateTime IssuedAt);

/// <summary>
/// Validates and manages short codes for partner scan fallback.
/// </summary>
public interface IShortCodeValidator
{
    /// <summary>
    /// Validates and consumes a short code atomically.
    /// Code is deleted after successful validation (one-time use).
    /// </summary>
    Task<ShortCodeValidationResult> ValidateAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Gets existing OTP for a user, or generates a new one if none exists.
    /// Returns the code and remaining TTL.
    /// </summary>
    Task<ShortCodeGenerationResult> GetOrCreateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets existing OTP for a user without generation.
    /// Returns null if no active OTP exists.
    /// </summary>
    Task<ShortCodeGenerationResult?> GetExistingAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Redis-backed short code validator with persistent OTP per user.
/// </summary>
public sealed class ShortCodeValidator : IShortCodeValidator
{
    private const string CodeKeyPrefix = "auth:otp:code:";
    private const string UserKeyPrefix = "auth:otp:user:";
    private const string OtpEventName = "integration.otp.issued.v1";
    private static readonly TimeSpan CodeTtl = TimeSpan.FromSeconds(60);
    private static readonly Regex CodePattern = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly IConnectionMultiplexer _redis;
    private readonly IEventPublisher _events;
    private readonly ILogger<ShortCodeValidator> _logger;

    public ShortCodeValidator(
        IConnectionMultiplexer redis,
        IEventPublisher events,
        ILogger<ShortCodeValidator> logger)
    {
        _redis = redis;
        _events = events;
        _logger = logger;
    }

    public async Task<ShortCodeValidationResult> ValidateAsync(string code, CancellationToken ct = default)
    {
        var sanitized = Regex.Replace(code ?? "", @"[\s\-]", "");

        if (!CodePattern.IsMatch(sanitized))
        {
            return ShortCodeValidationResult.Fail("invalid_code_format");
        }

        var db = _redis.GetDatabase();
        var codeKey = $"{CodeKeyPrefix}{sanitized}";

        // Atomic get and delete (one-time use)
        var userId = await db.StringGetDeleteAsync(codeKey).ConfigureAwait(false);

        if (userId.IsNullOrEmpty)
        {
            _logger.LogDebug("Short code {Code} not found or expired", sanitized);
            return ShortCodeValidationResult.Fail("code_expired_or_invalid");
        }

        if (!Guid.TryParse(userId.ToString(), out var parsedUserId))
        {
            _logger.LogWarning("Short code {Code} has invalid userId value: {Value}", sanitized, userId);
            return ShortCodeValidationResult.Fail("invalid_code_data");
        }

        // Also delete user->code mapping
        var userKey = $"{UserKeyPrefix}{parsedUserId:D}";
        await db.KeyDeleteAsync(userKey).ConfigureAwait(false);

        _logger.LogInformation("Short code validated and consumed for user {UserId}", parsedUserId);
        return ShortCodeValidationResult.Ok(parsedUserId);
    }

    public async Task<ShortCodeGenerationResult?> GetExistingAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var db = _redis.GetDatabase();
        var userKey = $"{UserKeyPrefix}{userId:D}";

        var existingCode = await db.StringGetAsync(userKey).ConfigureAwait(false);
        if (existingCode.IsNullOrEmpty)
        {
            return null;
        }

        // Get remaining TTL
        var ttl = await db.KeyTimeToLiveAsync(userKey).ConfigureAwait(false);
        if (!ttl.HasValue || ttl.Value.TotalSeconds <= 0)
        {
            return null;
        }

        return ShortCodeGenerationResult.Ok(existingCode.ToString()!, (int)ttl.Value.TotalSeconds);
    }

    public async Task<ShortCodeGenerationResult> GetOrCreateAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return ShortCodeGenerationResult.Fail("invalid_user_id");
        }

        var db = _redis.GetDatabase();
        var userKey = $"{UserKeyPrefix}{userId:D}";

        // Check if user already has an active OTP
        var existingCode = await db.StringGetAsync(userKey).ConfigureAwait(false);
        if (!existingCode.IsNullOrEmpty)
        {
            var ttl = await db.KeyTimeToLiveAsync(userKey).ConfigureAwait(false);
            if (ttl.HasValue && ttl.Value.TotalSeconds > 0)
            {
                _logger.LogDebug("Returning existing OTP for user {UserId}, TTL={Ttl}s", userId, (int)ttl.Value.TotalSeconds);
                return ShortCodeGenerationResult.Ok(existingCode.ToString()!, (int)ttl.Value.TotalSeconds);
            }
        }

        // Generate new 6-digit code
        var code = Random.Shared.Next(100000, 999999).ToString();
        var codeKey = $"{CodeKeyPrefix}{code}";

        // Check collision and retry
        if (await db.KeyExistsAsync(codeKey).ConfigureAwait(false))
        {
            code = Random.Shared.Next(100000, 999999).ToString();
            codeKey = $"{CodeKeyPrefix}{code}";
        }

        // Store both keys with same TTL
        var batch = db.CreateBatch();
        var t1 = batch.StringSetAsync(codeKey, userId.ToString(), CodeTtl);
        var t2 = batch.StringSetAsync(userKey, code, CodeTtl);
        batch.Execute();
        
        await Task.WhenAll(t1, t2).ConfigureAwait(false);

        if (!await t1 || !await t2)
        {
            _logger.LogWarning("Failed to store OTP for user {UserId}", userId);
            return ShortCodeGenerationResult.Fail("storage_failed");
        }

        _logger.LogInformation("Generated new OTP {Code} for user {UserId}, expires in {Ttl}s", code, userId, CodeTtl.TotalSeconds);

        // Publish event for realtime
        try
        {
            var evt = new OtpIssuedEvent(userId, code, (int)CodeTtl.TotalSeconds, DateTime.UtcNow);
            await _events.PublishAsync(evt, OtpEventName, userId.ToString(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish OTP event for user {UserId}", userId);
        }

        return ShortCodeGenerationResult.Ok(code, (int)CodeTtl.TotalSeconds);
    }
}
