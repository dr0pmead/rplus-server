using System;

namespace RPlus.Kernel.Runtime.Domain.Entities;

public sealed class RuntimeGraphNodeState
{
    public Guid RuleId { get; set; }
    public Guid UserId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
