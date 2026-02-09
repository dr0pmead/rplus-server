// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Persistence.AuthDbContext
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Auth.Domain.Entities;
using System;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Auth.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext((DbContextOptions) options)
{
  public DbSet<AuthUserEntity> AuthUsers { get; set; } = null!;

  public DbSet<AuthCredentialEntity> AuthCredentials { get; set; } = null!;

  public DbSet<AuthSessionEntity> AuthSessions { get; set; } = null!;

  public DbSet<AuthRecoveryEntity> AuthRecoveries { get; set; } = null!;

  public DbSet<RefreshTokenEntity> RefreshTokens { get; set; } = null!;

  public DbSet<AuditLogEntity> AuditLogs { get; set; } = null!;

  public DbSet<DeviceEntity> Devices { get; set; } = null!;

  public DbSet<AuthKnownUserEntity> AuthKnownUsers { get; set; } = null!;

  public DbSet<OtpChallengeEntity> OtpChallenges { get; set; } = null!;

  public DbSet<PasskeyCredentialEntity> PasskeyCredentials { get; set; } = null!;

  public DbSet<AbuseCounterEntity> AbuseCounters { get; set; } = null!;

  public DbSet<OutboxMessageEntity> OutboxMessages { get; set; } = null!;

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.HasDefaultSchema("auth");
    modelBuilder.Entity<AuthUserEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.Login).IsUnique();
      entity.HasIndex(e => e.PhoneHash).IsUnique();
    });
    modelBuilder.Entity<AuthCredentialEntity>(entity =>
    {
      entity.HasKey(e => e.UserId);
    });
    modelBuilder.Entity<AuthRecoveryEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.RecoveryHash).IsRequired();
      entity.Property(e => e.RecoverySalt).IsRequired();
      entity.HasOne(d => d.User)
        .WithMany()
        .HasForeignKey(d => d.UserId)
        .OnDelete(DeleteBehavior.Cascade);
    });
    modelBuilder.Entity<AuthSessionEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasOne(d => d.User)
        .WithMany()
        .HasForeignKey(d => d.UserId)
        .OnDelete(DeleteBehavior.Cascade);
    });
    modelBuilder.Entity<DeviceEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.DeviceKey);
    });
    modelBuilder.Entity<AuthKnownUserEntity>(entity =>
    {
      entity.HasKey(e => e.UserId);
    });
    modelBuilder.Entity<OtpChallengeEntity>(entity =>
    {
      entity.HasKey(e => e.Id);
    });
    modelBuilder.Entity<PasskeyCredentialEntity>(entity =>
    {
      entity.HasKey(e => e.DescriptorId);
      entity.HasOne(d => d.User)
        .WithMany()
        .HasForeignKey(d => d.UserId)
        .OnDelete(DeleteBehavior.Cascade);
    });
    modelBuilder.Entity<AbuseCounterEntity>(entity =>
    {
      entity.HasKey(e => e.Key);
    });
    modelBuilder.Entity<OutboxMessageEntity>(entity =>
    {
      entity.ToTable("outbox_messages");
      entity.HasKey(e => e.Id);
    });
  }
}
