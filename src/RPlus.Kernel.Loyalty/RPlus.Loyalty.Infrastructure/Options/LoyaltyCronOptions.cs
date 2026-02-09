namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltyCronOptions
{
    public const string SectionName = "Loyalty:Cron";

    public bool Enabled { get; set; } = false;

    /// <summary>Emit cron ticks at this interval (seconds). Default: 86400 (once per day).</summary>
    public int IntervalSeconds { get; set; } = 86400;
}
