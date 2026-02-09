// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.EffectiveSnapshotConfiguration
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Access.Domain.Entities;
using System;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Access.Infrastructure.Persistence.Configurations;

public class EffectiveSnapshotConfiguration : IEntityTypeConfiguration<EffectiveSnapshot>
{
  public void Configure(EntityTypeBuilder<EffectiveSnapshot> builder)
  {
    builder.ToTable<EffectiveSnapshot>("effective_snapshots");
    builder.HasKey((Expression<Func<EffectiveSnapshot, object>>) (x => (object) x.Id));
    builder.Property<Guid>((Expression<Func<EffectiveSnapshot, Guid>>) (x => x.Id)).HasColumnName<Guid>("id");
    builder.Property<Guid>((Expression<Func<EffectiveSnapshot, Guid>>) (x => x.UserId)).HasColumnName<Guid>("user_id");
    builder.Property<Guid>((Expression<Func<EffectiveSnapshot, Guid>>) (x => x.TenantId)).HasColumnName<Guid>("tenant_id");
    builder.Property<string>((Expression<Func<EffectiveSnapshot, string>>) (x => x.Context)).HasColumnName<string>("context");
    builder.Property<string>((Expression<Func<EffectiveSnapshot, string>>) (x => x.DataJson)).HasColumnName<string>("data_json").IsRequired(true);
    builder.Property<long>((Expression<Func<EffectiveSnapshot, long>>) (x => x.Version)).HasColumnName<long>("version");
    builder.Property<DateTime>((Expression<Func<EffectiveSnapshot, DateTime>>) (x => x.CalculatedAt)).HasColumnName<DateTime>("calculated_at");
    builder.Property<DateTime>((Expression<Func<EffectiveSnapshot, DateTime>>) (x => x.ExpiresAt)).HasColumnName<DateTime>("expires_at");
    builder.HasIndex((Expression<Func<EffectiveSnapshot, object>>) (x => new
    {
      UserId = x.UserId,
      TenantId = x.TenantId,
      Context = x.Context
    })).IsUnique(true);
  }
}
