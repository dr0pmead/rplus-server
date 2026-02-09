// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.Migrations.UsersDbContextModelSnapshot
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Users.Infrastructure.Persistence;
using System;

#nullable enable
namespace RPlus.Users.Infrastructure.Migrations;

[DbContext(typeof (UsersDbContext))]
internal class UsersDbContextModelSnapshot : ModelSnapshot
{
  protected override void BuildModel(
  #nullable disable
  ModelBuilder modelBuilder)
  {
    modelBuilder.HasDefaultSchema("users").HasAnnotation("ProductVersion", (object) "10.0.1").HasAnnotation("Relational:MaxIdentifierLength", (object) 63 /*0x3F*/);
    modelBuilder.UseIdentityByDefaultColumns();
    modelBuilder.Entity("RPlus.Users.Domain.Entities.OutboxMessageEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("AggregateId").IsRequired(true).HasMaxLength(128 /*0x80*/).HasColumnType<string>("character varying(128)");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("Error").HasColumnType<string>("text");
      b.Property<string>("EventType").IsRequired(true).HasMaxLength((int) byte.MaxValue).HasColumnType<string>("character varying(255)");
      b.Property<string>("LockedBy").HasColumnType<string>("text");
      b.Property<DateTime?>("LockedUntil").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<int>("MaxRetries").HasColumnType<int>("integer");
      b.Property<DateTime?>("NextRetryAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("Payload").IsRequired(true).HasColumnType<string>("jsonb");
      b.Property<DateTime?>("ProcessedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<int>("RetryCount").HasColumnType<int>("integer");
      b.Property<string>("Status").IsRequired(true).HasMaxLength(50).HasColumnType<string>("character varying(50)");
      b.Property<string>("Topic").IsRequired(true).HasMaxLength((int) byte.MaxValue).HasColumnType<string>("character varying(255)");
      b.HasKey("Id");
      b.HasIndex("Status", "CreatedAt");
      b.ToTable("OutboxMessages", "users");
    }));
    modelBuilder.Entity("RPlus.Users.Domain.Entities.UserEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("AvatarId").HasColumnType<string>("text");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("FirstName").IsRequired(true).HasMaxLength(100).HasColumnType<string>("character varying(100)");
      b.Property<string>("LastName").IsRequired(true).HasMaxLength(100).HasColumnType<string>("character varying(100)");
      b.Property<string>("Locale").IsRequired(true).HasMaxLength(10).HasColumnType<string>("character varying(10)");
      b.Property<string>("MiddleName").HasMaxLength(100).HasColumnType<string>("character varying(100)");
      b.Property<string>("PreferredName").HasMaxLength(100).HasColumnType<string>("character varying(100)");
      b.Property<string>("Status").IsRequired(true).HasMaxLength(20).HasColumnType<string>("character varying(20)");
      b.Property<string>("TimeZone").IsRequired(true).HasMaxLength(50).HasColumnType<string>("character varying(50)");
      b.Property<DateTime>("UpdatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.HasKey("Id");
      b.ToTable("Users", "users");
    }));
  }
}
