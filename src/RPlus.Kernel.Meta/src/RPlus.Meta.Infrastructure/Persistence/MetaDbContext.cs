using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;

namespace RPlus.Meta.Infrastructure.Persistence;

public sealed class MetaDbContext : DbContext, IMetaDbContext
{
    public MetaDbContext(DbContextOptions<MetaDbContext> options) : base(options)
    {
    }

    public DbSet<MetaEntityType> EntityTypes => Set<MetaEntityType>();
    public DbSet<MetaFieldDefinition> FieldDefinitions => Set<MetaFieldDefinition>();
    public DbSet<MetaFieldType> FieldTypes => Set<MetaFieldType>();
    public DbSet<MetaEntityRecord> Records => Set<MetaEntityRecord>();
    public DbSet<MetaFieldValue> FieldValues => Set<MetaFieldValue>();
    public DbSet<MetaRelation> Relations => Set<MetaRelation>();
    public DbSet<MetaList> Lists => Set<MetaList>();
    public DbSet<MetaListItem> ListItems => Set<MetaListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("meta");

        modelBuilder.Entity<MetaEntityType>(entity =>
        {
            entity.ToTable("meta_entity_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(1024);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<MetaFieldDefinition>(entity =>
        {
            entity.ToTable("meta_field_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.DataType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.OptionsJson).HasColumnType("jsonb");
            entity.Property(x => x.ValidationJson).HasColumnType("jsonb");
            entity.Property(x => x.ReferenceSourceJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.EntityTypeId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<MetaFieldType>(entity =>
        {
            entity.ToTable("meta_field_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.UiSchemaJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<MetaEntityRecord>(entity =>
        {
            entity.ToTable("meta_entity_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SubjectType).HasMaxLength(128);
        });

        modelBuilder.Entity<MetaFieldValue>(entity =>
        {
            entity.ToTable("meta_field_values");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ValueJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.RecordId, x.FieldId }).IsUnique();
        });

        modelBuilder.Entity<MetaRelation>(entity =>
        {
            entity.ToTable("meta_relations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RelationType).IsRequired().HasMaxLength(128);
            entity.HasIndex(x => new { x.FromRecordId, x.ToRecordId, x.RelationType }).IsUnique();
        });

        modelBuilder.Entity<MetaList>(entity =>
        {
            entity.ToTable("meta_lists");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(1024);
            entity.Property(x => x.SyncMode).IsRequired().HasMaxLength(32);
            entity.Property(x => x.EntityTypeId);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => x.EntityTypeId);
        });

        modelBuilder.Entity<MetaListItem>(entity =>
        {
            entity.ToTable("meta_list_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.ValueJson).HasColumnType("jsonb");
            entity.Property(x => x.ExternalId).HasMaxLength(128);
            entity.Property(x => x.OrganizationNodeId);
            entity.HasIndex(x => new { x.ListId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.ListId, x.ExternalId }).IsUnique();
            entity.HasIndex(x => new { x.ListId, x.OrganizationNodeId });
        });
    }
}

public interface IMetaDbContext
{
    DbSet<MetaEntityType> EntityTypes { get; }
    DbSet<MetaFieldDefinition> FieldDefinitions { get; }
    DbSet<MetaFieldType> FieldTypes { get; }
    DbSet<MetaEntityRecord> Records { get; }
    DbSet<MetaFieldValue> FieldValues { get; }
    DbSet<MetaRelation> Relations { get; }
    DbSet<MetaList> Lists { get; }
    DbSet<MetaListItem> ListItems { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
