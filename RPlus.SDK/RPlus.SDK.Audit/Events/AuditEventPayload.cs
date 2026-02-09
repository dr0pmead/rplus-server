using System;
using System.Collections.Generic;
using RPlus.SDK.Audit.Enums;

#nullable enable
namespace RPlus.SDK.Audit.Events;

public sealed class AuditEventPayload
{
    public Guid EventId { get; init; }

    public EventSource Source { get; init; }

    public AuditEventType EventType { get; init; }

    public EventSeverity Severity { get; init; }

    public string Actor { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Resource { get; init; } = string.Empty;

    public Dictionary<string, object>? Metadata { get; init; }

    public DateTime Timestamp { get; init; }

    public string PreviousEventHash { get; init; } = string.Empty;

    public string? Signature { get; init; }

    public string? SignerId { get; init; }
}
