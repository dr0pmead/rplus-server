// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent.RecordAuditEventCommand
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using MediatR;
using RPlus.Kernel.Audit.Domain.ValueObjects;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;

public record RecordAuditEventCommand(
  EventSource Source,
  AuditEventType EventType,
  EventSeverity Severity,
  string Actor,
  string Action,
  string Resource,
  Dictionary<string, object>? Metadata = null) : IRequest<RecordAuditEventResponse>, IBaseRequest
;
