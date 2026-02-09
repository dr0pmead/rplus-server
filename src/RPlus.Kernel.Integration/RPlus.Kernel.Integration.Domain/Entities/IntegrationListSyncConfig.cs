namespace RPlus.Kernel.Integration.Domain.Entities;

public sealed class IntegrationListSyncConfig
{
    public Guid Id { get; set; }
    public Guid IntegrationId { get; set; }
    public Guid ListId { get; set; }
    public bool IsEnabled { get; set; }
    public bool AllowDelete { get; set; }
    public bool Strict { get; set; }
    public string MappingJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
