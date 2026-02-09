// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.SodPolicySetConfiguration
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

public class SodPolicySetConfiguration : IEntityTypeConfiguration<SodPolicySet>
{
  public void Configure(EntityTypeBuilder<SodPolicySet> builder)
  {
    builder.ToTable<SodPolicySet>("sod_policy_sets");
    builder.HasKey((Expression<Func<SodPolicySet, object>>) (x => (object) x.Id));
    builder.Property<Guid>((Expression<Func<SodPolicySet, Guid>>) (x => x.Id)).HasColumnName<Guid>("id");
    builder.Property<Guid?>((Expression<Func<SodPolicySet, Guid?>>) (x => x.TenantId)).HasColumnName<Guid?>("tenant_id");
    builder.Property<int>((Expression<Func<SodPolicySet, int>>) (x => x.Version)).HasColumnName<int>("version");
    builder.Property<SodPolicyStatus>((Expression<Func<SodPolicySet, SodPolicyStatus>>) (x => x.Status)).HasColumnName<SodPolicyStatus>("status");
    builder.Property<Guid>((Expression<Func<SodPolicySet, Guid>>) (x => x.CreatedBy)).HasColumnName<Guid>("created_by");
    builder.Property<DateTime>((Expression<Func<SodPolicySet, DateTime>>) (x => x.CreatedAt)).HasColumnName<DateTime>("created_at");
    builder.Property<Guid?>((Expression<Func<SodPolicySet, Guid?>>) (x => x.ApprovedBy)).HasColumnName<Guid?>("approved_by");
    builder.Property<DateTime?>((Expression<Func<SodPolicySet, DateTime?>>) (x => x.ApprovedAt)).HasColumnName<DateTime?>("approved_at");
    builder.Property<byte[]>((Expression<Func<SodPolicySet, byte[]>>) (x => x.RowVersion))
      .HasColumnName<byte[]>("row_version")
      .HasDefaultValueSql("decode(md5(random()::text), 'hex')")
      .IsRowVersion();
  }
}
