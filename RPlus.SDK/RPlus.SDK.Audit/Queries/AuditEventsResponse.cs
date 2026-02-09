using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Audit.Queries;

public sealed class AuditEventsResponse
{
    public List<AuditEventDto> Events { get; init; } = new();

    public int TotalCount { get; init; }
}
