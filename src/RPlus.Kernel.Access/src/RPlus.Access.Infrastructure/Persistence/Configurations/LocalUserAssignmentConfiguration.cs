// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.LocalUserAssignmentConfiguration
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

public class LocalUserAssignmentConfiguration : IEntityTypeConfiguration<LocalUserAssignment>
{
  public void Configure(EntityTypeBuilder<LocalUserAssignment> builder)
  {
    builder.ToTable<LocalUserAssignment>("local_user_assignments");
    builder.HasKey((Expression<Func<LocalUserAssignment, object>>) (x => new
    {
      TenantId = x.TenantId,
      UserId = x.UserId,
      NodeId = x.NodeId,
      RoleCode = x.RoleCode
    }));
    builder.Property<Guid>((Expression<Func<LocalUserAssignment, Guid>>) (x => x.TenantId)).HasColumnName<Guid>("tenant_id");
    builder.Property<Guid>((Expression<Func<LocalUserAssignment, Guid>>) (x => x.UserId)).HasColumnName<Guid>("user_id");
    builder.Property<Guid>((Expression<Func<LocalUserAssignment, Guid>>) (x => x.NodeId)).HasColumnName<Guid>("node_id");
    builder.Property<string>((Expression<Func<LocalUserAssignment, string>>) (x => x.RoleCode)).HasColumnName<string>("role_code").HasMaxLength(100);
    builder.Property<string>((Expression<Func<LocalUserAssignment, string>>) (x => x.PathSnapshot)).HasColumnName<string>("path_snapshot").HasColumnType<string>("ltree").IsRequired(true);
    builder.HasIndex((Expression<Func<LocalUserAssignment, object>>) (x => x.PathSnapshot)).HasMethod<LocalUserAssignment>("gist");
  }
}
