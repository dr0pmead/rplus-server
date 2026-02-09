namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltyMetaOptions
{
    public const string SectionName = "Loyalty:Meta";

    public string GrpcAddress { get; set; } = "http://rplus-kernel-meta:5019";

    public string? ServiceSecret { get; set; }

    public int CacheSeconds { get; set; } = 60;
}
