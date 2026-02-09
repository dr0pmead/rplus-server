namespace RPlus.SDK.Integration.Contracts;

public class IntegrationStatEvent
{
    public Guid PartnerId { get; set; }
    public Guid KeyId { get; set; }
    public string Env { get; set; } = string.Empty; // live, test
    public string? Scope { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public int Status { get; set; }
    public long LatencyMs { get; set; }
    public Guid CorrelationId { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
