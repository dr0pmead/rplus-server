// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Persistence.Configurations.AuthCredentialConfiguration
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Auth.Domain.Entities;
using System;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Auth.Infrastructure.Persistence.Configurations;

public class AuthCredentialConfiguration : IEntityTypeConfiguration<AuthCredentialEntity>
{
  public void Configure(EntityTypeBuilder<AuthCredentialEntity> builder)
  {
    builder.ToTable<AuthCredentialEntity>("auth_credentials");
    builder.HasKey((Expression<Func<AuthCredentialEntity, object>>) (x => (object) x.UserId));
    builder.Property<Guid>((Expression<Func<AuthCredentialEntity, Guid>>) (x => x.UserId)).HasColumnName<Guid>("user_id").IsRequired(true);
    builder.Property<byte[]>((Expression<Func<AuthCredentialEntity, byte[]>>) (x => x.PasswordHash)).HasColumnName<byte[]>("password_hash").IsRequired(true);
    builder.Property<byte[]>((Expression<Func<AuthCredentialEntity, byte[]>>) (x => x.PasswordSalt)).HasColumnName<byte[]>("password_salt").IsRequired(true);
    builder.Property<DateTime>((Expression<Func<AuthCredentialEntity, DateTime>>) (x => x.ChangedAt)).HasColumnName<DateTime>("changed_at").IsRequired(true);
    builder.Property<DateTime>((Expression<Func<AuthCredentialEntity, DateTime>>) (x => x.CreatedAt)).HasColumnName<DateTime>("created_at").IsRequired(true);
  }
}
