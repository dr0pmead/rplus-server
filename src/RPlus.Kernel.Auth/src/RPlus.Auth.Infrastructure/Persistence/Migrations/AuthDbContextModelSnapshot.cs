// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Persistence.Migrations.AuthDbContextModelSnapshot
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Auth.Infrastructure.Persistence;
using System;

#nullable enable
namespace RPlus.Auth.Persistence.Migrations;

[DbContext(typeof (AuthDbContext))]
internal class AuthDbContextModelSnapshot : ModelSnapshot
{
  protected override void BuildModel(
  #nullable disable
  ModelBuilder modelBuilder)
  {
    modelBuilder.HasDefaultSchema("auth").HasAnnotation("ProductVersion", (object) "10.0.1").HasAnnotation("Relational:MaxIdentifierLength", (object) 63 /*0x3F*/);
    modelBuilder.UseIdentityByDefaultColumns();
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AbuseCounterEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<string>("Key").HasColumnType<string>("text");
      b.Property<int>("Counter").HasColumnType<int>("integer");
      b.Property<DateTime>("WindowExpiresAt").HasColumnType<DateTime>("timestamp with time zone");
      b.HasKey("Key");
      b.ToTable("AbuseCounters", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuditLogEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("Action").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("DeviceId").HasColumnType<string>("text");
      b.Property<string>("ErrorCode").HasColumnType<string>("text");
      b.Property<string>("ErrorMessage").HasColumnType<string>("text");
      b.Property<string>("Ip").HasColumnType<string>("text");
      b.Property<bool>("IsSuspicious").HasColumnType<bool>("boolean");
      b.Property<string>("Location").HasColumnType<string>("text");
      b.Property<string>("MetadataJson").HasColumnType<string>("text");
      b.Property<string>("PhoneHash").HasColumnType<string>("text");
      b.Property<string>("Result").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("RiskLevel").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("UserAgent").HasColumnType<string>("text");
      b.Property<Guid?>("UserId").HasColumnType<Guid?>("uuid");
      b.HasKey("Id");
      b.ToTable("AuditLogs", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthCredentialEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("UserId").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid").HasColumnName<Guid>("user_id");
      b.Property<DateTime>("ChangedAt").HasColumnType<DateTime>("timestamp with time zone").HasColumnName<DateTime>("changed_at");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone").HasColumnName<DateTime>("created_at");
      b.Property<byte[]>("PasswordHash").IsRequired(true).HasColumnType<byte[]>("bytea").HasColumnName<byte[]>("password_hash");
      b.Property<byte[]>("PasswordSalt").IsRequired(true).HasColumnType<byte[]>("bytea").HasColumnName<byte[]>("password_salt");
      b.HasKey("UserId");
      b.ToTable("auth_credentials", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthKnownUserEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("UserId").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<bool>("IsActive").HasColumnType<bool>("boolean");
      b.Property<string>("PhoneHash").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime>("UpdatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.HasKey("UserId");
      b.ToTable("AuthKnownUsers", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthRecoveryEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<byte[]>("RecoveryHash").IsRequired(true).HasColumnType<byte[]>("bytea");
      b.Property<byte[]>("RecoverySalt").IsRequired(true).HasColumnType<byte[]>("bytea");
      b.Property<Guid>("UserId").HasColumnType<Guid>("uuid");
      b.HasKey("Id");
      b.HasIndex("UserId");
      b.ToTable("AuthRecoveries", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthSessionEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("DeviceFingerprint").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("DeviceId").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("DeviceName").HasColumnType<string>("text");
      b.Property<string>("DeviceOs").HasColumnType<string>("text");
      b.Property<DateTime>("ExpiresAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<bool>("IsSuspicious").HasColumnType<bool>("boolean");
      b.Property<DateTime>("IssuedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("IssuerIp").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("IssuerLocation").HasColumnType<string>("text");
      b.Property<string>("IssuerUserAgent").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime?>("LastActivityAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<bool>("RequiresMfa").HasColumnType<bool>("boolean");
      b.Property<string>("RevokeReason").HasColumnType<string>("text");
      b.Property<DateTime?>("RevokedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("RiskLevel").IsRequired(true).HasColumnType<string>("text");
      b.Property<int>("RiskScore").HasColumnType<int>("integer");
      b.Property<string>("SuspiciousActivityDetails").HasColumnType<string>("text");
      b.Property<Guid>("UserId").HasColumnType<Guid>("uuid");
      b.HasKey("Id");
      b.HasIndex("UserId");
      b.ToTable("AuthSessions", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthUserEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("BlockReason").HasColumnType<string>("text");
      b.Property<DateTime?>("BlockedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("Email").HasColumnType<string>("text");
      b.Property<int>("FailedAttempts").HasColumnType<int>("integer");
      b.Property<bool>("IsBlocked").HasColumnType<bool>("boolean");
      b.Property<DateTime?>("LastLoginAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<DateTime?>("LastOtpSentAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<DateTime?>("LockedUntil").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("Login").HasColumnType<string>("text");
      b.Property<int>("PasswordVersion").HasColumnType<int>("integer");
      b.Property<string>("PhoneEncrypted").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("PhoneHash").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("RegistrationDeviceId").HasColumnType<string>("text");
      b.Property<string>("RegistrationIp").HasColumnType<string>("text");
      b.Property<string>("RegistrationUserAgent").HasColumnType<string>("text");
      b.Property<int>("SecurityVersion").HasColumnType<int>("integer");
      b.Property<Guid>("TenantId").HasColumnType<Guid>("uuid");
      b.Property<int>("UserType").HasColumnType<int>("integer");
      b.HasKey("Id");
      b.HasIndex("Login").IsUnique();
      b.HasIndex("PhoneHash").IsUnique();
      b.ToTable("AuthUsers", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.DeviceEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("DeviceKey").IsRequired(true).HasColumnType<string>("text");
      b.Property<bool>("IsBlocked").HasColumnType<bool>("boolean");
      b.Property<DateTime>("LastSeenAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("PublicJwk").HasColumnType<string>("text");
      b.Property<Guid>("UserId").HasColumnType<Guid>("uuid");
      b.HasKey("Id");
      b.HasIndex("DeviceKey");
      b.ToTable("Devices", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.OtpChallengeEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<int>("AttemptsLeft").HasColumnType<int>("integer");
      b.Property<DateTime?>("BlockedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("ChallengeType").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("CodeHash").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<DateTime?>("DeliveredAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("DeliveryChannel").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("DeliveryError").HasColumnType<string>("text");
      b.Property<string>("DeliveryStatus").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime>("ExpiresAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<bool>("IsBlocked").HasColumnType<bool>("boolean");
      b.Property<string>("IssuerDeviceId").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("IssuerIp").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("PhoneHash").IsRequired(true).HasColumnType<string>("text");
      b.Property<Guid?>("UserId").HasColumnType<Guid?>("uuid");
      b.Property<DateTime?>("VerifiedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.HasKey("Id");
      b.ToTable("OtpChallenges", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.OutboxMessageEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("AggregateId").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime>("CreatedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("ErrorMessage").HasColumnType<string>("text");
      b.Property<string>("ErrorStackTrace").HasColumnType<string>("text");
      b.Property<string>("EventType").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("LockedBy").HasColumnType<string>("text");
      b.Property<DateTime?>("LockedUntil").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<int>("MaxRetries").HasColumnType<int>("integer");
      b.Property<DateTime?>("NextRetryAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("Payload").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime?>("ProcessedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<int>("RetryCount").HasColumnType<int>("integer");
      b.Property<DateTime?>("SentAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<string>("Status").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("Topic").IsRequired(true).HasColumnType<string>("text");
      b.HasKey("Id");
      b.ToTable("outbox_messages", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.PasskeyCredentialEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<byte[]>("DescriptorId").HasColumnType<byte[]>("bytea");
      b.Property<Guid>("AaGuid").HasColumnType<Guid>("uuid");
      b.Property<string>("CredType").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("DeviceName").HasColumnType<string>("text");
      b.Property<byte[]>("PublicKey").IsRequired(true).HasColumnType<byte[]>("bytea");
      b.Property<DateTime>("RegDate").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<long>("SignatureCounter").HasColumnType<long>("bigint");
      b.Property<byte[]>("UserHandle").IsRequired(true).HasColumnType<byte[]>("bytea");
      b.Property<Guid>("UserId").HasColumnType<Guid>("uuid");
      b.HasKey("DescriptorId");
      b.HasIndex("UserId");
      b.ToTable("PasskeyCredentials", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.RefreshTokenEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType<Guid>("uuid");
      b.Property<string>("DeviceFingerprint").IsRequired(true).HasColumnType<string>("text");
      b.Property<Guid>("DeviceId").HasColumnType<Guid>("uuid");
      b.Property<string>("DpopThumbprint").HasColumnType<string>("text");
      b.Property<DateTime>("ExpiresAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<DateTime>("IssuedAt").HasColumnType<DateTime>("timestamp with time zone");
      b.Property<string>("LastIp").HasColumnType<string>("text");
      b.Property<string>("LastUserAgent").HasColumnType<string>("text");
      b.Property<Guid?>("ReplacedById").HasColumnType<Guid?>("uuid");
      b.Property<DateTime?>("RevokedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<Guid>("SessionId").HasColumnType<Guid>("uuid");
      b.Property<string>("TokenFamily").IsRequired(true).HasColumnType<string>("text");
      b.Property<string>("TokenHash").IsRequired(true).HasColumnType<string>("text");
      b.Property<DateTime?>("UsedAt").HasColumnType<DateTime?>("timestamp with time zone");
      b.Property<Guid>("UserId").HasColumnType<Guid>("uuid");
      b.HasKey("Id");
      b.HasIndex("DeviceId");
      b.ToTable("RefreshTokens", "auth");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthRecoveryEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.HasOne("RPlus.Auth.Domain.Entities.AuthUserEntity", "User").WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
      b.Navigation("User");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.AuthSessionEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.HasOne("RPlus.Auth.Domain.Entities.AuthUserEntity", "User").WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
      b.Navigation("User");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.PasskeyCredentialEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.HasOne("RPlus.Auth.Domain.Entities.AuthUserEntity", "User").WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
      b.Navigation("User");
    }));
    modelBuilder.Entity("RPlus.Auth.Domain.Entities.RefreshTokenEntity", (Action<EntityTypeBuilder>) (b =>
    {
      b.HasOne("RPlus.Auth.Domain.Entities.DeviceEntity", "Device").WithMany().HasForeignKey("DeviceId").OnDelete(DeleteBehavior.Cascade).IsRequired();
      b.Navigation("Device");
    }));
  }
}
