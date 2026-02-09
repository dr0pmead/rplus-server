namespace RPlus.SDK.AI;

/// <summary>
/// Rate limiter for AI requests.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Check if request is allowed and consume a token.
    /// </summary>
    Task<RateLimitResult> TryAcquireAsync(
        string key,
        CancellationToken ct = default);

    /// <summary>
    /// Get current rate limit status without consuming.
    /// </summary>
    Task<RateLimitStatus> GetStatusAsync(
        string key,
        CancellationToken ct = default);
}

/// <summary>
/// Rate limit check result.
/// </summary>
public sealed record RateLimitResult(
    bool IsAllowed,
    int RemainingTokens,
    TimeSpan? RetryAfter = null);

/// <summary>
/// Current rate limit status.
/// </summary>
public sealed record RateLimitStatus(
    int Limit,
    int Remaining,
    DateTime ResetsAt);

/// <summary>
/// Rate limit configuration.
/// </summary>
public sealed record RateLimitConfig
{
    /// <summary>
    /// Requests per window.
    /// </summary>
    public int RequestsPerWindow { get; init; } = 60;

    /// <summary>
    /// Window duration.
    /// </summary>
    public TimeSpan WindowDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Tokens per request (for token bucket).
    /// </summary>
    public int TokensPerRequest { get; init; } = 1;

    /// <summary>
    /// Burst limit (max tokens in bucket).
    /// </summary>
    public int BurstLimit { get; init; } = 10;
}
