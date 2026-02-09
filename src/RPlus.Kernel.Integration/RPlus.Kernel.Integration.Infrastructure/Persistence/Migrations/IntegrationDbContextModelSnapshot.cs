// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Persistence.Migrations.IntegrationDbContextModelSnapshot
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Persistence.Migrations;

[DbContext(typeof (IntegrationDbContext))]
internal class IntegrationDbContextModelSnapshot : ModelSnapshot
{
  protected override void BuildModel(
  #nullable disable
  ModelBuilder modelBuilder)
  {
    modelBuilder.HasDefaultSchema("access").HasAnnotation("ProductVersion", (object) "10.0.1").HasAnnotation("Relational:MaxIdentifierLength", (object) 63 /*0x3F*/);
    modelBuilder.UseIdentityByDefaultColumns();
    modelBuilder.Entity("RPlus.Kernel.Integration.Domain.Entities.IntegrationApiKey", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<int>("Environment").HasColumnType<int>("integer");
      b.Property<DateTime?>("ExpiresAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("KeyHash").IsRequired(true).HasMaxLength(128 /*0x80*/).HasColumnType<string>("character varying(128)");
      b.Property<DateTime?>("LastUsedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<Guid>("PartnerId").HasColumnType<Guid>("uuid");
      b.Property<string>("Prefix").IsRequired(true).HasMaxLength(64 /*0x40*/).HasColumnType<string>("character varying(64)");
      b.Property<string>("RateLimits").IsRequired(true).HasColumnType<string>("jsonb");
      b.Property<bool>("RequireSignature").HasColumnType<bool>("boolean");
      b.Property<string>("Scopes").IsRequired(true).HasColumnType<string>("jsonb");
      b.Property<string>("SecretProtected").IsRequired(true).HasColumnType<string>("text");
      b.Property<int>("Status").HasColumnType<int>("integer");
      b.HasKey("Id");
      b.HasIndex("ExpiresAt");
      b.HasIndex("PartnerId");
      b.HasIndex("Status");
      b.HasIndex("Environment", "KeyHash");
      b.ToTable("integration_api_keys", "access");
    }));
    modelBuilder.Entity("RPlus.Kernel.Integration.Domain.Entities.IntegrationPartner", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("Description").HasMaxLength(512 /*0x0200*/).HasColumnType<string>("character varying(512)");
      b.Property<bool>("IsActive").HasColumnType<bool>("boolean");
      b.Property<string>("Name").IsRequired(true).HasMaxLength((int) byte.MaxValue).HasColumnType<string>("character varying(255)");
      b.HasKey("Id");
      b.ToTable("integration_partners", "access");
    }));
    modelBuilder.Entity("RPlus.Kernel.Integration.Domain.Entities.IntegrationRoute", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<bool>("IsActive").HasColumnType<bool>("boolean");
      b.Property<string>("Name").IsRequired(true).HasMaxLength(128 /*0x80*/).HasColumnType<string>("character varying(128)");
      b.Property<int>("Priority").HasColumnType<int>("integer");
      b.Property<string>("RoutePattern").IsRequired(true).HasMaxLength(256 /*0x0100*/).HasColumnType<string>("character varying(256)");
      b.Property<string>("TargetHost").IsRequired(true).HasMaxLength(256 /*0x0100*/).HasColumnType<string>("character varying(256)");
      b.Property<string>("TargetMethod").IsRequired(true).HasMaxLength(128 /*0x80*/).HasColumnType<string>("character varying(128)");
      b.Property<string>("TargetService").IsRequired(true).HasMaxLength(256 /*0x0100*/).HasColumnType<string>("character varying(256)");
      b.Property<string>("Transport").IsRequired(true).HasMaxLength(16 /*0x10*/).HasColumnType<string>("character varying(16)");
      b.HasKey("Id");
      b.HasIndex("IsActive");
      b.HasIndex("Priority");
      b.HasIndex("RoutePattern");
      b.ToTable("integration_routes", "access");
    }));
    modelBuilder.Entity("RPlus.Kernel.Integration.Domain.Entities.IntegrationStat", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType<long>("bigint").HasColumnName<long>("id");
      b.Property<long>("Id").UseIdentityByDefaultColumn<long>();
      b.Property<string>("CorrelationId").IsRequired(true).HasMaxLength(64 /*0x40*/).HasColumnType<string>("character varying(64)").HasColumnName<string>("correlation_id");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone").HasColumnName<DateTime>("created_at");
      b.Property<string>("Endpoint").IsRequired(true).HasMaxLength(256 /*0x0100*/).HasColumnType<string>("character varying(256)").HasColumnName<string>("endpoint");
      b.Property<string>("Env").IsRequired(true).HasMaxLength(16 /*0x10*/).HasColumnType<string>("character varying(16)").HasColumnName<string>("environment");
      b.Property<int>("ErrorCode").HasColumnType<int>("integer").HasColumnName<int>("error_code");
      b.Property<Guid>("KeyId").HasColumnType<Guid>("uuid").HasColumnName<Guid>("key_id");
      b.Property<long>("LatencyMs").HasColumnType<long>("bigint").HasColumnName<long>("latency_ms");
      b.Property<Guid>("PartnerId").HasColumnType<Guid>("uuid").HasColumnName<Guid>("partner_id");
      b.Property<string>("Scope").IsRequired(true).HasMaxLength(128 /*0x80*/).HasColumnType<string>("character varying(128)").HasColumnName<string>("scope");
      b.Property<int>("StatusCode").HasColumnType<int>("integer").HasColumnName<int>("status_code");
      b.HasKey("Id");
      b.HasIndex("CreatedAt");
      b.HasIndex("PartnerId");
      b.ToTable("integration_stats", "access");
    }));
    modelBuilder.Entity("RPlus.Kernel.Integration.Domain.Entities.IntegrationApiKey", (Action<EntityTypeBuilder>) (b =>
    {
      b.HasOne("RPlus.Kernel.Integration.Domain.Entities.IntegrationPartner", "Partner").WithMany().HasForeignKey("PartnerId").OnDelete(DeleteBehavior.Cascade).IsRequired();
      b.Navigation("Partner");
    }));
  }
}
