// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.Configurations.RoleConfiguration
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

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
  public void Configure(EntityTypeBuilder<Role> builder)
  {
    builder.ToTable<Role>("roles");
    builder.HasKey((Expression<Func<Role, object>>) (x => (object) x.Id));
    builder.Property<Guid>((Expression<Func<Role, Guid>>) (x => x.Id)).HasColumnName<Guid>("id");
    builder.Property<string>((Expression<Func<Role, string>>) (x => x.Code)).HasColumnName<string>("code").IsRequired(true).HasMaxLength(50);
    builder.HasIndex((Expression<Func<Role, object>>) (x => x.Code)).IsUnique(true);
    builder.Property<string>((Expression<Func<Role, string>>) (x => x.Name)).HasColumnName<string>("name").IsRequired(true).HasMaxLength(200);
  }
}
