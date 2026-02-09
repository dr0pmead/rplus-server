// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.PolicyAssignmentConfiguration
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

public class PolicyAssignmentConfiguration : IEntityTypeConfiguration<PolicyAssignment>
{
  public void Configure(EntityTypeBuilder<PolicyAssignment> builder)
  {
    builder.ToTable<PolicyAssignment>("policy_assignments");
    builder.HasKey((Expression<Func<PolicyAssignment, object>>) (x => (object) x.Id));
    builder.Property<Guid>((Expression<Func<PolicyAssignment, Guid>>) (x => x.Id)).HasColumnName<Guid>("id");
    builder.Property<Guid>((Expression<Func<PolicyAssignment, Guid>>) (x => x.TenantId)).HasColumnName<Guid>("tenant_id");
    builder.Property<string>((Expression<Func<PolicyAssignment, string>>) (x => x.TargetType)).HasColumnName<string>("target_type").IsRequired(true).HasMaxLength(50);
    builder.Property<string>((Expression<Func<PolicyAssignment, string>>) (x => x.TargetId)).HasColumnName<string>("target_id").IsRequired(true).HasMaxLength(100);
    builder.Property<string>((Expression<Func<PolicyAssignment, string>>) (x => x.PermissionId)).HasColumnName<string>("permission_id").IsRequired(true).HasMaxLength(150);
    builder.Property<string>((Expression<Func<PolicyAssignment, string>>) (x => x.Effect)).HasColumnName<string>("effect").IsRequired(true).HasMaxLength(10).HasDefaultValue<string>((object) "ALLOW");
    builder.Property<DateTime?>((Expression<Func<PolicyAssignment, DateTime?>>) (x => x.ExpiresAt)).HasColumnName<DateTime?>("expires_at");
    builder.Property<DateTime>((Expression<Func<PolicyAssignment, DateTime>>) (x => x.CreatedAt)).HasColumnName<DateTime>("created_at");
    builder.HasIndex((Expression<Func<PolicyAssignment, object>>) (x => new
    {
      TenantId = x.TenantId,
      TargetType = x.TargetType,
      TargetId = x.TargetId,
      PermissionId = x.PermissionId
    })).IsUnique(true);
    builder.HasOne<Permission>((Expression<Func<PolicyAssignment, Permission>>) (x => x.Permission)).WithMany((string) null).HasForeignKey((Expression<Func<PolicyAssignment, object>>) (x => x.PermissionId)).HasPrincipalKey((Expression<Func<Permission, object>>) (p => p.Id));
  }
}
