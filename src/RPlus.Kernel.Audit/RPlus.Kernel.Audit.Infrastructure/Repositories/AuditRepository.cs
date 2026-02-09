// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Infrastructure.Repositories.AuditRepository
// Assembly: RPlus.Kernel.Audit.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 271DD6D6-68D7-47FD-8F9A-65D4B328CF02
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Domain.Entities;
using RPlus.Kernel.Audit.Domain.ValueObjects;
using RPlus.Kernel.Audit.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Audit.Infrastructure.Repositories;

public class AuditRepository : IAuditRepository
{
  private readonly AuditDbContext _context;

  public AuditRepository(AuditDbContext context) => this._context = context;

  public async Task AddAsync(AuditEvent auditEvent)
  {
    EntityEntry<AuditEvent> entityEntry = await this._context.AuditEvents.AddAsync(auditEvent);
    int num = await this._context.SaveChangesAsync();
  }

  public async Task<AuditEvent?> GetByIdAsync(Guid id)
  {
    return await this._context.AuditEvents.FindAsync((object) id);
  }

  public async Task<List<AuditEvent>> GetEventsAsync(
    EventSource? source,
    DateTime? since,
    DateTime? until,
    int limit = 100)
  {
    IQueryable<AuditEvent> source1 = this._context.AuditEvents.AsQueryable();
    if (source.HasValue)
      source1 = source1.Where<AuditEvent>((Expression<Func<AuditEvent, bool>>) (e => (int) e.Source == (int) source.Value));
    if (since.HasValue)
      source1 = source1.Where<AuditEvent>((Expression<Func<AuditEvent, bool>>) (e => e.Timestamp >= since.Value));
    if (until.HasValue)
      source1 = source1.Where<AuditEvent>((Expression<Func<AuditEvent, bool>>) (e => e.Timestamp <= until.Value));
    return await source1.OrderByDescending<AuditEvent, DateTime>((Expression<Func<AuditEvent, DateTime>>) (e => e.Timestamp)).Take<AuditEvent>(limit).ToListAsync<AuditEvent>();
  }

  public Task<bool> ExistsAsync(Guid id)
  {
    return _context.AuditEvents.AnyAsync(e => e.Id == id);
  }
}
