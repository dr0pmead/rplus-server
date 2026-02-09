namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationStatsRetentionOptions
{
    public int RawRetentionDays { get; set; } = 30;

    public int DailyRetentionMonths { get; set; } = 24;

    public int RollupIntervalHours { get; set; } = 24;

    public int RollupLookbackDays { get; set; } = 2;
}
