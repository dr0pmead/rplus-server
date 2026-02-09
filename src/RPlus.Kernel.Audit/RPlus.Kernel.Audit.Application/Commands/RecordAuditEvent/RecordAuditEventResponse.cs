// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent.RecordAuditEventResponse
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using System;

#nullable disable
namespace RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;

public class RecordAuditEventResponse
{
  public Guid EventId { get; set; }

  public bool Success { get; set; }
}
