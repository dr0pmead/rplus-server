// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Queries.GetAuditEvents.AuditEventsResponse
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Audit.Application.Queries.GetAuditEvents;

public class AuditEventsResponse
{
  public List<AuditEventDto> Events { get; set; } = new List<AuditEventDto>();

  public int TotalCount { get; set; }
}
