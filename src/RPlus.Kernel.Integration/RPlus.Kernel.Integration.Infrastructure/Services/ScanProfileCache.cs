using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Infrastructure.Services;

/// <summary>
/// Cached scan profile data stored in Redis as Hash for atomic field updates.
/// v3.0: Uses CurrentLevel, TotalLevels, RPlusDiscount for scalable discount system.
/// </summary>
public record ScanProfile
{
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string? MiddleName { get; init; }
    public string? AvatarUrl { get; init; }
    /// <summary>User's current level (1-based index).</summary>
    public int CurrentLevel { get; init; } = 1;
    /// <summary>Total number of levels in the system.</summary>
    public int TotalLevels { get; init; } = 1;
    /// <summary>Ready-to-use RPlus discount (base + motivation), calculated by Loyalty.</summary>
    public decimal RPlusDiscount { get; init; }
    /// <summary>[Deprecated v2] Use CurrentLevel instead.</summary>
    public int Level { get; init; } = 1;
    /// <summary>[Deprecated v2] Now included in RPlusDiscount.</summary>
    public decimal MotivationBonus { get; init; }
    /// <summary>[Deprecated v1] Use RPlusDiscount instead.</summary>
    public decimal DiscountUser { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Interface for scan profile caching operations.
/// Uses Redis Hash for atomic field-level updates to handle concurrent HR and Loyalty events.
/// </summary>
public interface IScanProfileCache
{
    /// <summary>
    /// Get complete profile from cache.
    /// </summary>
    Task<ScanProfile?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Set complete profile (used by fallback aggregator).
    /// </summary>
    Task SetAsync(Guid userId, ScanProfile profile, CancellationToken ct = default);

    /// <summary>
    /// [Deprecated v1] Atomically update only discount field (from Loyalty events).
    /// Use PatchV3Async instead for v3.0 scalable discount system.
    /// </summary>
    Task PatchDiscountAsync(Guid userId, decimal discountUser, CancellationToken ct = default);

    /// <summary>
    /// [Deprecated v2] Atomically update Level and MotivationBonus.
    /// Use PatchV3Async instead.
    /// </summary>
    Task PatchLevelAsync(Guid userId, int level, decimal motivationBonus, CancellationToken ct = default);

    /// <summary>
    /// v3.0: Atomically update CurrentLevel, TotalLevels, RPlusDiscount (from Loyalty events).
    /// </summary>
    Task PatchV3Async(Guid userId, int currentLevel, int totalLevels, decimal rplusDiscount, CancellationToken ct = default);

    /// <summary>
    /// Atomically update only user info fields (from HR events).
    /// </summary>
    Task PatchUserInfoAsync(Guid userId, string firstName, string lastName, string? middleName, string? avatarUrl, CancellationToken ct = default);
}

/// <summary>
/// Redis Hash-based implementation of scan profile cache.
/// Uses HSET/HMGET for atomic field-level updates to avoid race conditions 
/// between HR and Loyalty consumers updating different fields.
/// </summary>
public sealed class ScanProfileCache : IScanProfileCache
{
    private const string KeyPrefix = "scan:profile:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    // Hash field names
    private const string FieldFirstName = "fn";
    private const string FieldLastName = "ln";
    private const string FieldMiddleName = "mn";
    private const string FieldAvatarUrl = "av";
    private const string FieldCurrentLevel = "cl";  // v3.0
    private const string FieldTotalLevels = "tl";   // v3.0
    private const string FieldRPlusDiscount = "rd"; // v3.0
    private const string FieldLevel = "lv";         // Deprecated v2
    private const string FieldMotivationBonus = "mb"; // Deprecated v2
    private const string FieldDiscountUser = "du";  // Deprecated v1
    private const string FieldUpdatedAt = "ua";

    private readonly IConnectionMultiplexer _redis;

    public ScanProfileCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<ScanProfile?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + userId;

        var values = await db.HashGetAsync(key, new RedisValue[]
        {
            FieldFirstName,     // 0
            FieldLastName,      // 1
            FieldMiddleName,    // 2
            FieldAvatarUrl,     // 3
            FieldCurrentLevel,  // 4 - v3.0
            FieldTotalLevels,   // 5 - v3.0
            FieldRPlusDiscount, // 6 - v3.0
            FieldLevel,         // 7 - deprecated v2
            FieldMotivationBonus, // 8 - deprecated v2
            FieldDiscountUser,  // 9 - deprecated v1
            FieldUpdatedAt      // 10
        }).ConfigureAwait(false);

        // If no data exists (all null), return null
        if (values[0].IsNullOrEmpty && values[4].IsNullOrEmpty && values[7].IsNullOrEmpty && values[9].IsNullOrEmpty)
            return null;

        // Extend TTL on read (keep-alive)
        _ = db.KeyExpireAsync(key, DefaultTtl, CommandFlags.FireAndForget);

        // Parse with fallback: v3 fields take priority, fall back to v2, then v1
        var currentLevel = values[4].IsNullOrEmpty 
            ? (values[7].IsNullOrEmpty ? 1 : int.Parse(values[7].ToString()!))
            : int.Parse(values[4].ToString()!);
        var totalLevels = values[5].IsNullOrEmpty ? 1 : int.Parse(values[5].ToString()!);
        var rplusDiscount = values[6].IsNullOrEmpty
            ? (values[8].IsNullOrEmpty && values[9].IsNullOrEmpty 
                ? 0m 
                : (values[8].IsNullOrEmpty ? 0m : decimal.Parse(values[8].ToString()!)) + 
                  (values[9].IsNullOrEmpty ? 0m : decimal.Parse(values[9].ToString()!)))
            : decimal.Parse(values[6].ToString()!);

        return new ScanProfile
        {
            FirstName = values[0].ToString() ?? "",
            LastName = values[1].ToString() ?? "",
            MiddleName = values[2].IsNullOrEmpty ? null : values[2].ToString(),
            AvatarUrl = values[3].IsNullOrEmpty ? null : values[3].ToString(),
            CurrentLevel = currentLevel,
            TotalLevels = totalLevels,
            RPlusDiscount = rplusDiscount,
            // Deprecated fields for backwards compatibility
            Level = values[7].IsNullOrEmpty ? currentLevel : int.Parse(values[7].ToString()!),
            MotivationBonus = values[8].IsNullOrEmpty ? 0 : decimal.Parse(values[8].ToString()!),
            DiscountUser = values[9].IsNullOrEmpty ? rplusDiscount : decimal.Parse(values[9].ToString()!),
            UpdatedAt = values[10].IsNullOrEmpty ? DateTime.UtcNow : DateTime.Parse(values[10].ToString()!)
        };
    }

    public async Task SetAsync(Guid userId, ScanProfile profile, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + userId;

        var entries = new HashEntry[]
        {
            new(FieldFirstName, profile.FirstName),
            new(FieldLastName, profile.LastName),
            new(FieldMiddleName, profile.MiddleName ?? ""),
            new(FieldAvatarUrl, profile.AvatarUrl ?? ""),
            new(FieldLevel, profile.Level.ToString()),
            new(FieldMotivationBonus, profile.MotivationBonus.ToString("F2")),
            new(FieldDiscountUser, profile.DiscountUser.ToString("F2")),
            new(FieldUpdatedAt, profile.UpdatedAt.ToString("O"))
        };

        await db.HashSetAsync(key, entries).ConfigureAwait(false);
        await db.KeyExpireAsync(key, DefaultTtl).ConfigureAwait(false);
    }

    public async Task PatchDiscountAsync(Guid userId, decimal discountUser, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + userId;

        // Legacy method - kept for backwards compatibility
        await db.HashSetAsync(key, new HashEntry[]
        {
            new(FieldDiscountUser, discountUser.ToString("F2")),
            new(FieldUpdatedAt, DateTime.UtcNow.ToString("O"))
        }).ConfigureAwait(false);

        await db.KeyExpireAsync(key, DefaultTtl).ConfigureAwait(false);
    }

    public async Task PatchLevelAsync(Guid userId, int level, decimal motivationBonus, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + userId;

        // New method for dynamic level-based discount system
        await db.HashSetAsync(key, new HashEntry[]
        {
            new(FieldLevel, level.ToString()),
            new(FieldMotivationBonus, motivationBonus.ToString("F2")),
            new(FieldUpdatedAt, DateTime.UtcNow.ToString("O"))
        }).ConfigureAwait(false);

        await db.KeyExpireAsync(key, DefaultTtl).ConfigureAwait(false);
    }

    public async Task PatchUserInfoAsync(
        Guid userId,
        string firstName,
        string lastName,
        string? middleName,
        string? avatarUrl,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + userId;

        await db.HashSetAsync(key, new HashEntry[]
        {
            new(FieldFirstName, firstName),
            new(FieldLastName, lastName),
            new(FieldMiddleName, middleName ?? ""),
            new(FieldAvatarUrl, avatarUrl ?? ""),
            new(FieldUpdatedAt, DateTime.UtcNow.ToString("O"))
        }).ConfigureAwait(false);

        await db.KeyExpireAsync(key, DefaultTtl).ConfigureAwait(false);
    }

    /// <summary>
    /// v3.0: Atomically update CurrentLevel, TotalLevels, RPlusDiscount.
    /// </summary>
    public async Task PatchV3Async(Guid userId, int currentLevel, int totalLevels, decimal rplusDiscount, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + userId;

        await db.HashSetAsync(key, new HashEntry[]
        {
            new(FieldCurrentLevel, currentLevel.ToString()),
            new(FieldTotalLevels, totalLevels.ToString()),
            new(FieldRPlusDiscount, rplusDiscount.ToString("F2")),
            // Also update deprecated fields for backwards compatibility
            new(FieldLevel, currentLevel.ToString()),
            new(FieldDiscountUser, rplusDiscount.ToString("F2")),
            new(FieldUpdatedAt, DateTime.UtcNow.ToString("O"))
        }).ConfigureAwait(false);

        await db.KeyExpireAsync(key, DefaultTtl).ConfigureAwait(false);
    }
}

