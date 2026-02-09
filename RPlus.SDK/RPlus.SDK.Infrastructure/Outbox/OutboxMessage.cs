using System;

namespace RPlus.SDK.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AggregateId { get; set; } // Optional partition key
}
