using System;

namespace RPlus.SDK.Infrastructure.Idempotency;

public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
