using System;

namespace RPlus.Kernel.Runtime.Domain.Entities;

public sealed class RuntimeGraphExecution
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public Guid UserId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public decimal PointsDelta { get; set; }
    public string ActionsJson { get; set; } = "[]";
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
