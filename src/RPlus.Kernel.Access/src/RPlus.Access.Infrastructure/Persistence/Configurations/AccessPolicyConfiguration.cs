// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.AccessPolicyConfiguration
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

public class AccessPolicyConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
  public void Configure(EntityTypeBuilder<AccessPolicy> builder)
  {
    builder.ToTable<AccessPolicy>("access_policies");
    builder.HasKey((Expression<Func<AccessPolicy, object>>) (x => (object) x.Id));
    builder.Property<Guid>((Expression<Func<AccessPolicy, Guid>>) (x => x.Id)).HasColumnName<Guid>("id");
    builder.Property<string>((Expression<Func<AccessPolicy, string>>) (x => x.Effect)).HasColumnName<string>("effect").HasDefaultValue<string>((object) "ALLOW").HasMaxLength(10);
    builder.Property<string>((Expression<Func<AccessPolicy, string>>) (x => x.ScopeType)).HasColumnName<string>("scope_type").IsRequired(true).HasMaxLength(20);
    builder.Property<string>((Expression<Func<AccessPolicy, string>>) (x => x.ConditionExpression)).HasColumnName<string>("condition_expression");
    builder.Property<int?>((Expression<Func<AccessPolicy, int?>>) (x => x.RequiredAuthLevel)).HasColumnName<int?>("required_auth_level");
    builder.Property<int?>((Expression<Func<AccessPolicy, int?>>) (x => x.MaxAuthAgeSeconds)).HasColumnName<int?>("max_auth_age_seconds");
    builder.Property<DateTime>((Expression<Func<AccessPolicy, DateTime>>) (x => x.CreatedAt)).HasColumnName<DateTime>("created_at");
    builder.Property<byte[]>((Expression<Func<AccessPolicy, byte[]>>) (x => x.RowVersion))
      .HasColumnName<byte[]>("row_version")
      .HasDefaultValueSql("decode(md5(random()::text), 'hex')")
      .IsRowVersion();
    builder.Property<int>((Expression<Func<AccessPolicy, int>>) (x => x.Priority)).HasColumnName<int>("priority").HasDefaultValue<int>((object) 0);
    builder.Property<Guid>((Expression<Func<AccessPolicy, Guid>>) (x => x.TenantId)).HasColumnName<Guid>("tenant_id").HasDefaultValue<Guid>((object) Guid.Empty);
    builder.HasOne<Role>((Expression<Func<AccessPolicy, Role>>) (x => x.Role)).WithMany("Policies").HasForeignKey((Expression<Func<AccessPolicy, object>>) (x => (object) x.RoleId));
    builder.Property<Guid>((Expression<Func<AccessPolicy, Guid>>) (x => x.RoleId)).HasColumnName<Guid>("role_id");
    builder.Property<string>((Expression<Func<AccessPolicy, string>>) (x => x.PermissionId)).HasColumnName<string>("permission_id").IsRequired(true).HasMaxLength(150);
    builder.HasOne<Permission>((Expression<Func<AccessPolicy, Permission>>) (x => x.Permission)).WithMany((string) null).HasForeignKey((Expression<Func<AccessPolicy, object>>) (x => x.PermissionId)).HasPrincipalKey((Expression<Func<Permission, object>>) (p => p.Id));
  }
}
