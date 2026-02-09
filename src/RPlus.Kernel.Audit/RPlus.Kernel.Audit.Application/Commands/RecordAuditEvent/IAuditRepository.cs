// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent.IAuditRepository
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using RPlus.Kernel.Audit.Domain.Entities;
using RPlus.Kernel.Audit.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;

public interface IAuditRepository
{
  Task AddAsync(AuditEvent auditEvent);

  Task<AuditEvent?> GetByIdAsync(Guid id);

  Task<List<AuditEvent>> GetEventsAsync(
    EventSource? source,
    DateTime? since,
    DateTime? until,
    int limit = 100);

  Task<bool> ExistsAsync(Guid id);
}
