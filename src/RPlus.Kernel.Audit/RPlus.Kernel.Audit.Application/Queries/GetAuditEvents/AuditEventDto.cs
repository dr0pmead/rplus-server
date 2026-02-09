// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Queries.GetAuditEvents.AuditEventDto
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Audit.Application.Queries.GetAuditEvents;

public class AuditEventDto
{
  public Guid Id { get; set; }

  public string Source { get; set; } = string.Empty;

  public string EventType { get; set; } = string.Empty;

  public string Severity { get; set; } = string.Empty;

  public string Actor { get; set; } = string.Empty;

  public string Action { get; set; } = string.Empty;

  public string Resource { get; set; } = string.Empty;

  public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

  public DateTime Timestamp { get; set; }
}
