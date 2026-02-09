using System;

namespace RPlus.Loyalty.Domain.Entities;

/// <summary>
/// Raw inbox of incoming Kafka events consumed by Loyalty (v2).
/// Used for debugging, audit, and deterministic re-processing in the future.
/// </summary>
public sealed class LoyaltyIngressEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Topic { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string OperationId { get; set; } = string.Empty;

    public string? EventType { get; set; }

    public Guid UserId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Raw JSON message value as received from Kafka.</summary>
    public string PayloadJson { get; set; } = "{}";

    public DateTime? ProcessedAt { get; set; }

    public decimal PointsAwarded { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}

