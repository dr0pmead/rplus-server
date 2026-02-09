namespace RPlus.Loyalty.Infrastructure.Options;

public sealed class LoyaltySchedulerOptions
{
    public const string SectionName = "Loyalty:Scheduler";

    public bool Enabled { get; set; } = false;

    public int PollSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 25;

    public int LockSeconds { get; set; } = 30;
}

