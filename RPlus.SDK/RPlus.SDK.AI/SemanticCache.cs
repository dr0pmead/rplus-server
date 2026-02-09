namespace RPlus.SDK.AI;

/// <summary>
/// Semantic cache for instant responses to frequent queries.
/// </summary>
public interface ISemanticCache
{
    /// <summary>
    /// Try to get cached response for semantically similar query.
    /// </summary>
    Task<SemanticCacheEntry?> GetAsync(
        string query,
        float threshold = 0.95f,
        CancellationToken ct = default);

    /// <summary>
    /// Cache a response for future similar queries.
    /// </summary>
    Task SetAsync(
        string query,
        string response,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    Task<SemanticCacheStats> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Cached response entry.
/// </summary>
public sealed record SemanticCacheEntry(
    string Query,
    string Response,
    float Similarity,
    DateTime CachedAt);

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed record SemanticCacheStats(
    long TotalHits,
    long TotalMisses,
    int CachedItems,
    float HitRate);
