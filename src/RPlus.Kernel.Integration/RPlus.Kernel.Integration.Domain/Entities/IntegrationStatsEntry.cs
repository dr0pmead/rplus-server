using System;

namespace RPlus.Kernel.Integration.Domain.Entities;

public class IntegrationStatsEntry
{
    public long Id { get; set; }
    public Guid PartnerId { get; set; }
    public Guid KeyId { get; set; }
    public string Env { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
