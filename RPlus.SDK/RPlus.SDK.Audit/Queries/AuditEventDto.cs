using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Audit.Queries;

public sealed class AuditEventDto
{
    public Guid Id { get; init; }

    public string Source { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Actor { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Resource { get; init; } = string.Empty;

    public Dictionary<string, object> Metadata { get; init; } = new();

    public DateTime Timestamp { get; init; }
}
