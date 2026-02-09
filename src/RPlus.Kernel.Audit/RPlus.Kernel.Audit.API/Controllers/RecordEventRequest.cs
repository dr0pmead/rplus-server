// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Api.Controllers.RecordEventRequest
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B6E86C6-C04D-4A27-9376-A218083AF5B8
// Assembly location: F:\RPlus Framework\Recovery\audit\ExecuteService.dll

using RPlus.Kernel.Audit.Domain.ValueObjects;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Audit.Api.Controllers;

public record RecordEventRequest(
  EventSource Source,
  AuditEventType EventType,
  EventSeverity Severity,
  string Actor,
  string Action,
  string Resource,
  Dictionary<string, object>? Metadata = null)
;
