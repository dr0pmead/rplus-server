using System;
using System.Collections.Generic;
using RPlus.SDK.Audit.Enums;

#nullable enable
namespace RPlus.SDK.Audit.Models;

public class AuditEvent
{
    public Guid Id { get; set; }
    public EventSource Source { get; set; }
    public AuditEventType EventType { get; set; }
    public EventSeverity Severity { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string EventHash { get; set; } = string.Empty;
    public string PreviousEventHash { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public string? SignerId { get; set; }
}
