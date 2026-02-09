// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.AppConfiguration
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Access.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Access.Infrastructure.Persistence.Configurations;

public class AppConfiguration : IEntityTypeConfiguration<App>
{
  public void Configure(EntityTypeBuilder<App> builder)
  {
    builder.ToTable<App>("applications");
    builder.HasKey((Expression<Func<App, object>>) (x => (object) x.Id));
    builder.Property<Guid>((Expression<Func<App, Guid>>) (x => x.Id)).HasColumnName<Guid>("id");
    builder.Property<string>((Expression<Func<App, string>>) (x => x.Code)).HasColumnName<string>("code").IsRequired(true).HasMaxLength(50);
    builder.Property<string>((Expression<Func<App, string>>) (x => x.Name)).HasColumnName<string>("name");
    builder.HasIndex((Expression<Func<App, object>>) (x => x.Code)).IsUnique(true);
    builder.HasMany<Permission>((Expression<Func<App, IEnumerable<Permission>>>) (x => x.Permissions)).WithOne((Expression<Func<Permission, App>>) (x => x.App)).HasForeignKey((Expression<Func<Permission, object>>) (x => (object) x.AppId)).HasConstraintName<App, Permission>("fk_permissions_apps_app_id");
  }
}
