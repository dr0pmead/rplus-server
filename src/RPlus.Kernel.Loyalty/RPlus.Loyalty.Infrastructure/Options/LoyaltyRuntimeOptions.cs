namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltyRuntimeOptions
{
    public const string SectionName = "Loyalty:Runtime";
    public string? GrpcAddress { get; set; }
}
