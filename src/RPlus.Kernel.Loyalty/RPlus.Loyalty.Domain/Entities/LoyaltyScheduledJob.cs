using System;

namespace RPlus.Loyalty.Domain.Entities;

public sealed class LoyaltyScheduledJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RuleId { get; set; }

    public Guid UserId { get; set; }

    public DateTime RunAtUtc { get; set; }

    public string OperationId { get; set; } = string.Empty;

    public string? EventType { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

    public DateTime? LockedUntilUtc { get; set; }

    public string? LockedBy { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public decimal PointsAwarded { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }
}

