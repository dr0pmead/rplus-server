using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Audit.Enums;

#nullable enable
namespace RPlus.SDK.Audit.Commands;

public record RecordAuditEventCommand(
    EventSource Source,
    AuditEventType EventType,
    EventSeverity Severity,
    string Actor,
    string Action,
    string Resource,
    Dictionary<string, object>? Metadata = null) : IRequest<RecordAuditEventResponse>, IBaseRequest;
