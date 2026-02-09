// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.PermissionConfiguration
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

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
  public void Configure(EntityTypeBuilder<Permission> builder)
  {
    builder.ToTable<Permission>("features");
    builder.HasKey((Expression<Func<Permission, object>>) (x => x.Id));
    builder.Property<string>((Expression<Func<Permission, string>>) (x => x.Id)).HasColumnName<string>("id");
    builder.Property<Guid>((Expression<Func<Permission, Guid>>) (x => x.AppId)).HasColumnName<Guid>("app_id");
    builder.Property<string>((Expression<Func<Permission, string>>) (x => x.Resource)).HasColumnName<string>("resource").IsRequired(true).HasMaxLength(100);
    builder.Property<string>((Expression<Func<Permission, string>>) (x => x.Action)).HasColumnName<string>("action").IsRequired(true).HasMaxLength(100);
    builder.Property<string>((Expression<Func<Permission, string>>) (x => x.Title)).HasColumnName<string>("title").IsRequired(true).HasMaxLength(200);
    builder.Property<string>((Expression<Func<Permission, string>>) (x => x.Description)).HasColumnName<string>("description").HasMaxLength(1000);
    builder.Property<DateTime>((Expression<Func<Permission, DateTime>>) (x => x.CreatedAt)).HasColumnName<DateTime>("created_at");
    builder.Property<DateTime>((Expression<Func<Permission, DateTime>>) (x => x.UpdatedAt)).HasColumnName<DateTime>("updated_at");
    builder.Property<string>((Expression<Func<Permission, string>>) (x => x.Status)).HasColumnName<string>("status").HasMaxLength(20).HasDefaultValue<string>((object) "ACTIVE");
    builder.Property<string[]>((Expression<Func<Permission, string[]>>) (x => x.SupportedContexts)).HasColumnName<string[]>("supported_contexts").HasColumnType<string[]>("text[]");
    builder.Property<string?>((Expression<Func<Permission, string?>>) (x => x.SourceService)).HasColumnName<string>("source_service").HasMaxLength(100);
    builder.HasIndex((Expression<Func<Permission, object>>) (x => x.Id)).IsUnique(true);
    builder.HasIndex((Expression<Func<Permission, object>>) (x => x.SourceService));
  }
}
