using System;
using System.Text.Json;

namespace RPlus.Loyalty.Api.Responses;

public sealed class ScheduledJobDto
{
    public Guid Id { get; set; }

    public Guid RuleId { get; set; }

    public Guid UserId { get; set; }

    public DateTime RunAtUtc { get; set; }

    public string OperationId { get; set; } = string.Empty;

    public string? EventType { get; set; }

    public JsonElement Payload { get; set; }

    public string Status { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public decimal PointsAwarded { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}

