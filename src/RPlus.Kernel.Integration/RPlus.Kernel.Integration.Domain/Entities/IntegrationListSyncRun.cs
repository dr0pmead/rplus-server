namespace RPlus.Kernel.Integration.Domain.Entities;

public sealed class IntegrationListSyncRun
{
    public Guid Id { get; set; }
    public Guid IntegrationId { get; set; }
    public Guid ListId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string Mode { get; set; } = "upsert";
    public int ItemsCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
    public string? ErrorSamplesJson { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
}
