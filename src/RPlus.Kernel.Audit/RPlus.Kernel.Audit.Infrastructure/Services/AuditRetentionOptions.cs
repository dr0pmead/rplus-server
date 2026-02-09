namespace RPlus.Kernel.Audit.Infrastructure.Services;

public sealed class AuditRetentionOptions
{
    public int RetentionDays { get; set; } = 90;

    public int CleanupIntervalHours { get; set; } = 24;
}
