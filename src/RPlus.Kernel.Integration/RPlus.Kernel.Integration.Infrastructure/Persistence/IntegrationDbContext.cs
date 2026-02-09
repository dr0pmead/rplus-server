// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Persistence.IntegrationDbContext
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Persistence;

public class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : 
  DbContext((DbContextOptions) options),
  IIntegrationDbContext
{
  public DbSet<IntegrationRoute> Routes { get; set; } = null!;

  public DbSet<IntegrationPartner> Partners { get; set; } = null!;

  public DbSet<IntegrationApiKey> ApiKeys { get; set; } = null!;

  public DbSet<IntegrationAuditLog> AuditLogs { get; set; } = null!;

  public DbSet<IntegrationStatsEntry> Stats { get; set; } = null!;

  public DbSet<IntegrationListSyncConfig> ListSyncConfigs { get; set; } = null!;

  public DbSet<IntegrationListSyncRun> ListSyncRuns { get; set; } = null!;

  // Double Entry Partner Scan System
  public DbSet<PartnerScan> PartnerScans { get; set; } = null!;

  public DbSet<PartnerCommit> PartnerCommits { get; set; } = null!;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    if (optionsBuilder.IsConfigured)
      return;
    optionsBuilder.UseNpgsql().UseSnakeCaseNamingConvention();
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.HasDefaultSchema("integration");
    JsonSerializerOptions jsonOptions = new JsonSerializerOptions();
    ValueConverter<List<string>?, string> scopesConverter = new ValueConverter<List<string>?, string>(v => JsonSerializer.Serialize(v ?? new List<string>(), jsonOptions), v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());
    ValueConverter<List<string>?, string> profileFieldsConverter = new ValueConverter<List<string>?, string>(
      v => JsonSerializer.Serialize(v ?? new List<string>(), jsonOptions),
      v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());
    ValueConverter<Dictionary<string, object>?, string> metadataConverter = new ValueConverter<Dictionary<string, object>?, string>(
      v => JsonSerializer.Serialize(v ?? new Dictionary<string, object>(), jsonOptions),
      v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, jsonOptions) ?? new Dictionary<string, object>());
    ValueConverter<Dictionary<string, int>?, string> rateConverter = new ValueConverter<Dictionary<string, int>?, string>(v => JsonSerializer.Serialize(v ?? new Dictionary<string, int>(), jsonOptions), v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, jsonOptions) ?? new Dictionary<string, int>());
    modelBuilder.Entity<IntegrationPartner>(entity =>
    {
      entity.ToTable("partners", "integration");
      entity.HasKey(p => p.Id);
      entity.Property(p => p.Id).ValueGeneratedNever();
      entity.Property(p => p.Name).IsRequired().HasMaxLength(byte.MaxValue);
      entity.Property(p => p.Description).HasMaxLength(512);
      entity.Property(p => p.AccessLevel).IsRequired().HasMaxLength(32).HasDefaultValue("limited");
      entity.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);
      entity.Property(p => p.IsDiscountPartner).IsRequired().HasDefaultValue(false);
      entity.Property(p => p.DiscountPartner).HasColumnType("numeric");
      entity.Property(p => p.ProfileFields).HasColumnType("jsonb").HasConversion(profileFieldsConverter);
      entity.Property(p => p.Metadata).HasColumnType("jsonb").HasConversion(metadataConverter).HasDefaultValueSql("'{}'::jsonb");
      // Dynamic Level-Based Discount System
      entity.Property(p => p.DiscountStrategy).HasMaxLength(32).HasDefaultValue("dynamic_level");
      entity.Property(p => p.PartnerCategory).HasMaxLength(32).HasDefaultValue("retail");
      entity.Property(p => p.MaxDiscount).HasColumnType("numeric(5,2)");
      entity.Property(p => p.HappyHoursConfigJson).HasColumnType("jsonb");
      entity.Property(p => p.CreatedAt).IsRequired();
      entity.Property(p => p.DeletedAt);
    });
    modelBuilder.Entity<IntegrationApiKey>(entity =>
    {
      entity.ToTable("api_keys", "integration");
      entity.HasKey(k => k.Id);
      entity.Property(k => k.Id).ValueGeneratedNever();
      entity.Property(k => k.KeyHash).IsRequired().HasMaxLength(128);
      entity.Property(k => k.SecretProtected).IsRequired().HasMaxLength(512);
      entity.Property(k => k.Prefix).IsRequired().HasMaxLength(64);
      entity.Property(k => k.Environment).IsRequired().HasMaxLength(64);
      entity.Property(k => k.Status).IsRequired().HasMaxLength(32);
      entity.Property(k => k.RequireSignature).IsRequired().HasDefaultValue(false);
      entity.Property(k => k.CreatedAt).IsRequired();
      entity.Property(k => k.ExpiresAt);
      entity.Property(k => k.LastUsedAt);
      entity.Property(k => k.RevokedAt);
      entity.Property(k => k.Scopes).HasColumnType("jsonb").HasConversion(scopesConverter);
      entity.Property(k => k.RateLimits).HasColumnType("jsonb").HasConversion(rateConverter);
      entity.HasIndex(k => k.KeyHash).IsUnique();
      entity.HasIndex(k => k.PartnerId);
    });
    modelBuilder.Entity<IntegrationRoute>(entity =>
    {
      entity.ToTable("routes", "integration");
      entity.HasKey(r => r.Id);
      entity.Property(r => r.Id).ValueGeneratedNever();
      entity.Property(r => r.Name).IsRequired().HasMaxLength(128);
      entity.Property(r => r.RoutePattern).IsRequired().HasMaxLength(256);
      entity.Property(r => r.TargetHost).IsRequired().HasMaxLength(256);
      entity.Property(r => r.TargetService).IsRequired().HasMaxLength(256);
      entity.Property(r => r.TargetMethod).IsRequired().HasMaxLength(128);
      entity.Property(r => r.Transport).IsRequired().HasMaxLength(16);
      entity.Property(r => r.CreatedAt).IsRequired();
      entity.Property(r => r.PartnerId);
      entity.HasIndex(r => r.IsActive);
      entity.HasIndex(r => r.Priority);
      entity.HasIndex(r => r.PartnerId);
    });
    modelBuilder.Entity<IntegrationAuditLog>(entity =>
    {
      entity.ToTable("audit_logs", "integration");
      entity.HasKey(l => l.Id);
      entity.Property(l => l.Id).ValueGeneratedNever();
      entity.Property(l => l.TraceId).IsRequired().HasMaxLength(128);
      entity.Property(l => l.Timestamp).IsRequired();
      entity.Property(l => l.RequestPath).HasMaxLength(512);
      entity.Property(l => l.RequestMethod).HasMaxLength(16);
      entity.Property(l => l.TargetService).HasMaxLength(128);
      entity.Property(l => l.ClientIp).HasMaxLength(64);
      entity.Property(l => l.ErrorMessage).HasMaxLength(1024);
      entity.HasIndex(l => l.TraceId);
      entity.HasIndex(l => l.Timestamp);
      entity.HasIndex(l => l.ApiKeyId);
    });
    modelBuilder.Entity<IntegrationStatsEntry>(entity =>
    {
      entity.ToTable("integration_stats", "integration");
      entity.HasKey(s => s.Id);
      entity.Property(s => s.Id).UseIdentityByDefaultColumn();
      entity.Property(s => s.PartnerId).IsRequired();
      entity.Property(s => s.KeyId).IsRequired();
      entity.Property(s => s.Env).IsRequired().HasMaxLength(16);
      entity.Property(s => s.Scope).IsRequired().HasMaxLength(128);
      entity.Property(s => s.Endpoint).IsRequired().HasMaxLength(256);
      entity.Property(s => s.StatusCode).IsRequired();
      entity.Property(s => s.LatencyMs).IsRequired();
      entity.Property(s => s.CorrelationId).IsRequired().HasMaxLength(64);
      entity.Property(s => s.ErrorCode).IsRequired();
      entity.Property(s => s.CreatedAt).IsRequired();
      entity.HasIndex(s => s.CreatedAt);
      entity.HasIndex(s => s.PartnerId);
      entity.HasIndex(s => s.KeyId);
      entity.HasIndex(s => s.Scope);
    });

    modelBuilder.Entity<IntegrationListSyncConfig>(entity =>
    {
      entity.ToTable("list_sync_configs", "integration");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Id).ValueGeneratedNever();
      entity.Property(x => x.IntegrationId).IsRequired();
      entity.Property(x => x.ListId).IsRequired();
      entity.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(false);
      entity.Property(x => x.AllowDelete).IsRequired().HasDefaultValue(false);
      entity.Property(x => x.Strict).IsRequired().HasDefaultValue(false);
      entity.Property(x => x.MappingJson).IsRequired().HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
      entity.Property(x => x.CreatedAt).IsRequired();
      entity.Property(x => x.UpdatedAt).IsRequired();
      entity.HasIndex(x => new { x.IntegrationId, x.ListId }).IsUnique();
    });

    modelBuilder.Entity<IntegrationListSyncRun>(entity =>
    {
      entity.ToTable("list_sync_runs", "integration");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Id).ValueGeneratedNever();
      entity.Property(x => x.IntegrationId).IsRequired();
      entity.Property(x => x.ListId).IsRequired();
      entity.Property(x => x.ApiKeyId);
      entity.Property(x => x.Mode).IsRequired().HasMaxLength(16);
      entity.Property(x => x.ItemsCount).IsRequired();
      entity.Property(x => x.CreatedCount).IsRequired();
      entity.Property(x => x.UpdatedCount).IsRequired();
      entity.Property(x => x.DeletedCount).IsRequired();
      entity.Property(x => x.FailedCount).IsRequired();
      entity.Property(x => x.ErrorSamplesJson).HasColumnType("jsonb");
      entity.Property(x => x.StartedAt).IsRequired();
      entity.Property(x => x.FinishedAt);
      entity.Property(x => x.DurationMs);
      entity.HasIndex(x => x.IntegrationId);
      entity.HasIndex(x => x.ListId);
      entity.HasIndex(x => x.StartedAt);
    });

    // ========== Double Entry Partner Scan System ==========

    modelBuilder.Entity<PartnerScan>(entity =>
    {
      entity.ToTable("partner_scans", "integration");
      entity.HasKey(s => s.ScanId);
      entity.Property(s => s.ScanId).ValueGeneratedNever();
      
      entity.Property(s => s.PartnerId).IsRequired();
      entity.Property(s => s.TerminalId).IsRequired().HasMaxLength(255);
      entity.Property(s => s.CashierId).HasMaxLength(255);
      entity.Property(s => s.OrderId).IsRequired();
      entity.Property(s => s.OrderSumPredicted).HasColumnType("decimal(18,2)");
      entity.Property(s => s.UserId).IsRequired();
      entity.Property(s => s.ScanMethod).IsRequired().HasMaxLength(20).HasDefaultValue("qr");
      
      entity.Property(s => s.PredictedUserDiscount).HasColumnType("decimal(18,2)");
      entity.Property(s => s.PredictedPartnerDiscount).HasColumnType("decimal(18,2)");
      
      entity.Property(s => s.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
      entity.Property(s => s.ErrorReason).HasMaxLength(500);
      
      entity.Property(s => s.CreatedAt).IsRequired();
      entity.Property(s => s.ExpiresAt).IsRequired();
      entity.Property(s => s.CommittedAt);
      
      entity.Property(s => s.IdempotencyKey).IsRequired().HasMaxLength(128);
      entity.Property(s => s.TraceId).HasMaxLength(64);
      
      // Unique index for idempotency
      entity.HasIndex(s => new { s.PartnerId, s.IdempotencyKey }).IsUnique();
      entity.HasIndex(s => s.PartnerId);
      entity.HasIndex(s => s.UserId);
      entity.HasIndex(s => s.OrderId);
      entity.HasIndex(s => s.Status);
      entity.HasIndex(s => s.CreatedAt);
      
      // Navigation
      entity.HasOne(s => s.Partner)
        .WithMany()
        .HasForeignKey(s => s.PartnerId)
        .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<PartnerCommit>(entity =>
    {
      entity.ToTable("partner_commits", "integration");
      entity.HasKey(c => c.CommitId);
      entity.Property(c => c.CommitId).ValueGeneratedNever();
      
      entity.Property(c => c.ScanId).IsRequired();
      entity.Property(c => c.FinalOrderTotal).IsRequired().HasColumnType("decimal(18,2)");
      entity.Property(c => c.FinalUserDiscount).IsRequired().HasColumnType("decimal(18,2)");
      entity.Property(c => c.FinalPartnerDiscount).IsRequired().HasColumnType("decimal(18,2)");
      
      entity.Property(c => c.ChequeNumber).HasMaxLength(100);
      entity.Property(c => c.FiscalId).HasMaxLength(100);
      entity.Property(c => c.ClosedAt).IsRequired();
      
      entity.Property(c => c.ItemsJson).HasColumnType("jsonb");
      entity.Property(c => c.PaymentsJson).HasColumnType("jsonb");
      
      entity.Property(c => c.WalletProcessed).IsRequired().HasDefaultValue(false);
      entity.Property(c => c.WalletTransactionId);
      entity.Property(c => c.WalletProcessedAt);
      
      entity.Property(c => c.CreatedAt).IsRequired();
      entity.Property(c => c.IdempotencyKey).IsRequired().HasMaxLength(128);
      
      // Unique index on ScanId (only one commit per scan)
      entity.HasIndex(c => c.ScanId).IsUnique();
      entity.HasIndex(c => c.WalletProcessed);
      entity.HasIndex(c => c.CreatedAt);
      
      // Navigation
      entity.HasOne(c => c.Scan)
        .WithOne(s => s.Commit)
        .HasForeignKey<PartnerCommit>(c => c.ScanId)
        .OnDelete(DeleteBehavior.Restrict);
    });
  }
}
