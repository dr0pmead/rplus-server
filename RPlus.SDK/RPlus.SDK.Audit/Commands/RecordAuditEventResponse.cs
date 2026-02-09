using System;

#nullable enable
namespace RPlus.SDK.Audit.Commands;

public record RecordAuditEventResponse(Guid EventId, bool Success);
