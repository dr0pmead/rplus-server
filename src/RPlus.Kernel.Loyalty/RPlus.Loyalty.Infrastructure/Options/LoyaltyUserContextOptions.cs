namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltyUserContextOptions
{
    public const string SectionName = "Loyalty:UserContext";

    public bool Enabled { get; set; } = true;

    /// <summary>Redis cache TTL, in seconds.</summary>
    public int CacheSeconds { get; set; } = 300;

    /// <summary>gRPC address of Users service (plaintext HTTP/2).</summary>
    public string UsersGrpcAddress { get; set; } = "http://rplus-kernel-users:5014";

    /// <summary>gRPC address of HR service (plaintext HTTP/2).</summary>
    public string HrGrpcAddress { get; set; } = "http://rplus-kernel-hr:5016";

    /// <summary>Optional shared secret header for HR gRPC: x-rplus-service-secret.</summary>
    public string? HrSharedSecret { get; set; }

    /// <summary>HTTP base address of Organization service.</summary>
    public string OrganizationBaseAddress { get; set; } = "http://rplus-kernel-organization:5009/";

    /// <summary>Optional shared secret header for Organization HTTP: x-rplus-service-secret.</summary>
    public string? OrganizationSharedSecret { get; set; }

    /// <summary>Optional Redis key prefix for user context cache.</summary>
    public string RedisKeyPrefix { get; set; } = "rplus:loyalty:userctx:v1:";
}
