using Microsoft.EntityFrameworkCore;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Domain.Entities;
using System.Text.Json;

namespace RPlus.HR.Infrastructure.Persistence;

public sealed class HrDbContext(DbContextOptions<HrDbContext> options, IHrActorContext actorContext)
    : DbContext(options), IHrDbContext
{
    private readonly IHrActorContext _actorContext = actorContext;

    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    // Owned types are mapped to JSON, no separate DbSets
    // public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    // public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    // public DbSet<FamilyMemberDocument> FamilyMemberDocuments => Set<FamilyMemberDocument>();
    public DbSet<HrFile> HrFiles => Set<HrFile>();
    public DbSet<MilitaryRecord> MilitaryRecords => Set<MilitaryRecord>();
    public DbSet<BankDetails> BankDetails => Set<BankDetails>();
    public DbSet<HrCustomFieldDefinition> CustomFieldDefinitions => Set<HrCustomFieldDefinition>();
    public DbSet<HrCustomFieldValue> CustomFieldValues => Set<HrCustomFieldValue>();
    public DbSet<HrAuditLog> AuditLogs => Set<HrAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("hr");

        modelBuilder.Entity<EmployeeProfile>(entity =>
        {
            entity.ToTable("hr_profiles");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.Iin).HasMaxLength(12);
            entity.HasIndex(x => x.Iin).IsUnique();

            entity.Property(x => x.FirstName).HasMaxLength(128).HasDefaultValue(string.Empty);
            entity.Property(x => x.LastName).HasMaxLength(128).HasDefaultValue(string.Empty);
            entity.Property(x => x.MiddleName).HasMaxLength(128);

            entity.Property(x => x.PlaceOfBirth).HasMaxLength(256);
            entity.Property(x => x.Citizenship).HasMaxLength(2).HasDefaultValue("KZ");

            entity.Property(x => x.BloodType).HasMaxLength(16);
            entity.Property(x => x.ClothingSize).HasMaxLength(16);

            entity.Property(x => x.PersonalEmail).HasMaxLength(320);
            entity.Property(x => x.PersonalPhone).HasMaxLength(32);
            entity.Property(x => x.PhotoFileId);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(EmployeeStatus.Active);

            entity.Property(x => x.Gender)
                .HasConversion<string>()
                .HasMaxLength(16);

            entity.Property(x => x.DisabilityGroup)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(DisabilityGroup.None);

            entity.OwnsOne(x => x.RegistrationAddress, addr =>
            {
                addr.Property(x => x.Region).HasMaxLength(128);
                addr.Property(x => x.District).HasMaxLength(128);
                addr.Property(x => x.City).HasMaxLength(128);
                addr.Property(x => x.Street).HasMaxLength(256);
                addr.Property(x => x.Building).HasMaxLength(64);
                addr.Property(x => x.Flat).HasMaxLength(32);
            });

            entity.OwnsOne(x => x.ResidentialAddress, addr =>
            {
                addr.Property(x => x.Region).HasMaxLength(128);
                addr.Property(x => x.District).HasMaxLength(128);
                addr.Property(x => x.City).HasMaxLength(128);
                addr.Property(x => x.Street).HasMaxLength(256);
                addr.Property(x => x.Building).HasMaxLength(64);
                addr.Property(x => x.Flat).HasMaxLength(32);
            });

            // Documents stored as JSON column
            entity.OwnsMany(x => x.Documents, b =>
            {
                b.ToJson("documents");
            });

            // FamilyMembers stored as JSON column
            entity.OwnsMany(x => x.FamilyMembers, b =>
            {
                b.ToJson("family_members");
                
                b.OwnsMany(f => f.Documents);
            });

            entity.HasOne(x => x.MilitaryRecord)
                .WithOne()
                .HasForeignKey<MilitaryRecord>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.BankDetails)
                .WithOne()
                .HasForeignKey<BankDetails>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.CustomDataJson)
                .HasColumnName("custom_data_json")
                .HasColumnType("jsonb");

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.PhotoFileId);
        });

        // Removed Table Mappings for EmployeeDocument and FamilyMember
        /*
        modelBuilder.Entity<EmployeeDocument>(entity =>
        {
            ...
        });

        modelBuilder.Entity<FamilyMember>(entity =>
        {
            ...
        });
        */



        modelBuilder.Entity<HrFile>(entity =>
        {
            entity.ToTable("hr_files");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName).HasMaxLength(256);
            entity.Property(x => x.ContentType).HasMaxLength(128);
            entity.Property(x => x.Data).HasColumnType("bytea");

            entity.HasIndex(x => x.OwnerUserId);
        });

        modelBuilder.Entity<MilitaryRecord>(entity =>
        {
            entity.ToTable("hr_military_records");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.Rank).HasMaxLength(64);
            entity.Property(x => x.VusCode).HasMaxLength(64);
            entity.Property(x => x.LocalOffice).HasMaxLength(256);
        });

        modelBuilder.Entity<BankDetails>(entity =>
        {
            entity.ToTable("hr_bank_details");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.Iban).HasMaxLength(64);
            entity.Property(x => x.BankName).HasMaxLength(128);
        });

        modelBuilder.Entity<HrCustomFieldDefinition>(entity =>
        {
            entity.ToTable("hr_custom_fields");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Key).HasMaxLength(128);
            entity.HasIndex(x => x.Key).IsUnique();

            entity.Property(x => x.Label).HasMaxLength(256);
            entity.Property(x => x.Type).HasMaxLength(32);
            entity.Property(x => x.Group).HasMaxLength(64);
            entity.Property(x => x.IsSystem).HasDefaultValue(false);
            entity.Property(x => x.Pattern).HasMaxLength(256);
            entity.Property(x => x.Placeholder).HasMaxLength(256);
            entity.Property(x => x.OptionsJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.IsSystem);
            entity.HasIndex(x => x.Group);
        });

        modelBuilder.Entity<HrCustomFieldValue>(entity =>
        {
            entity.ToTable("hr_custom_values");
            entity.HasKey(x => new { x.UserId, x.FieldKey });

            entity.Property(x => x.FieldKey).HasMaxLength(128);
            entity.Property(x => x.ValueJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.FieldKey);
        });

        modelBuilder.Entity<HrAuditLog>(entity =>
        {
            entity.ToTable("hr_audit_logs");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ActorType).HasMaxLength(32);
            entity.Property(x => x.ActorService).HasMaxLength(128);
            entity.Property(x => x.EntityType).HasMaxLength(128);
            entity.Property(x => x.EntityId).HasMaxLength(128);
            entity.Property(x => x.Action).HasMaxLength(32);
            entity.Property(x => x.ChangesJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.ActorUserId);
            entity.HasIndex(x => x.EntityType);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TryAddAuditLogs();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void TryAddAuditLogs()
    {
        var actorType = string.IsNullOrWhiteSpace(_actorContext.ActorType) ? "unknown" : _actorContext.ActorType.Trim();
        var actorUserId = _actorContext.ActorUserId;
        var actorService = string.IsNullOrWhiteSpace(_actorContext.ActorService) ? null : _actorContext.ActorService.Trim();

        var now = DateTime.UtcNow;
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

        foreach (var entry in entries)
        {
            if (entry.Entity is HrAuditLog)
                continue;

            var (entityType, entityId) = ResolveEntityIdentity(entry.Entity);
            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
                continue;

            var action = entry.State switch
            {
                EntityState.Added => "create",
                EntityState.Modified => "update",
                EntityState.Deleted => "delete",
                _ => "unknown"
            };

            var fields = entry.Properties
                .Where(p => p.IsModified || entry.State == EntityState.Added)
                .Select(p => p.Metadata.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            var changesJson = JsonSerializer.Serialize(new { fields });

            AuditLogs.Add(new HrAuditLog
            {
                OccurredAt = now,
                ActorType = actorType,
                ActorUserId = actorUserId,
                ActorService = actorService,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                ChangesJson = changesJson
            });
        }
    }

    private static (string EntityType, string EntityId) ResolveEntityIdentity(object entity) =>
        entity switch
        {
            EmployeeProfile p => (nameof(EmployeeProfile), p.UserId.ToString()),
            EmployeeDocument d => (nameof(EmployeeDocument), d.Id.ToString()),
            FamilyMember f => (nameof(FamilyMember), f.Id.ToString()),
            FamilyMemberDocument fd => (nameof(FamilyMemberDocument), fd.Id.ToString()),
            HrFile file => (nameof(HrFile), file.Id.ToString()),
            MilitaryRecord m => (nameof(MilitaryRecord), m.UserId.ToString()),
            BankDetails b => (nameof(BankDetails), b.UserId.ToString()),
            HrCustomFieldDefinition f => (nameof(HrCustomFieldDefinition), f.Id.ToString()),
            HrCustomFieldValue v => (nameof(HrCustomFieldValue), $"{v.UserId}:{v.FieldKey}"),
            _ => (string.Empty, string.Empty)
        };
}
