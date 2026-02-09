// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Infrastructure.Persistence.AuditDbContext
// Assembly: RPlus.Kernel.Audit.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 271DD6D6-68D7-47FD-8F9A-65D4B328CF02
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Kernel.Audit.Domain.Entities;
using RPlus.Kernel.Audit.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Kernel.Audit.Infrastructure.Persistence;

public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext((DbContextOptions) options)
{
  public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<AuditEvent>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Source).IsRequired();
      entity.Property(e => e.EventType).IsRequired();
      entity.Property(e => e.Severity).IsRequired();
      entity.Property(e => e.Actor).IsRequired().HasMaxLength(256);
      entity.Property(e => e.Action).IsRequired().HasMaxLength(512);
      entity.Property(e => e.Resource).IsRequired().HasMaxLength(512);
      entity.Property(e => e.Timestamp).IsRequired();
      entity.Property(e => e.Metadata).HasColumnType("jsonb");
      entity.HasIndex(e => e.Timestamp);
      entity.HasIndex(e => e.Source);
      entity.HasIndex(e => e.EventType);
      entity.HasIndex(e => new
      {
        e.Source,
        e.Timestamp
      });
    });
  }
}
