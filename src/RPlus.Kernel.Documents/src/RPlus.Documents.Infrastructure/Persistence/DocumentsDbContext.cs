using Microsoft.EntityFrameworkCore;
using RPlus.Documents.Application.Interfaces;
using RPlus.Documents.Domain.Entities;

namespace RPlus.Documents.Infrastructure.Persistence;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options)
    : DbContext(options), IDocumentsDbContext
{
    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();
    public DbSet<DocumentFolder> DocumentFolders => Set<DocumentFolder>();
    public DbSet<DocumentFolderMember> DocumentFolderMembers => Set<DocumentFolderMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("documents");

        modelBuilder.Entity<DocumentFile>(entity =>
        {
            entity.ToTable("document_files");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName).HasMaxLength(256);
            entity.Property(x => x.ContentType).HasMaxLength(128);
            entity.Property(x => x.StorageKey).HasMaxLength(512);
            entity.Property(x => x.SubjectType).HasMaxLength(64);
            entity.Property(x => x.DocumentType).HasMaxLength(64);
            entity.Property(x => x.IsLocked);

            entity.HasIndex(x => x.FolderId);
            entity.HasIndex(x => x.OwnerUserId);
            entity.HasIndex(x => x.OrganizationId);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.SubjectType);
            entity.HasIndex(x => x.SubjectId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.DeletedAt);

            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<DocumentShare>(entity =>
        {
            entity.ToTable("document_shares");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.DocumentId);
            entity.HasIndex(x => x.GrantedToUserId);
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<DocumentFolder>(entity =>
        {
            entity.ToTable("document_folders");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.Type).HasMaxLength(32);

            entity.HasIndex(x => x.OwnerUserId);
            entity.HasIndex(x => x.OrganizationId);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.Type);
        });

        modelBuilder.Entity<DocumentFolderMember>(entity =>
        {
            entity.ToTable("document_folder_members");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.FolderId);
            entity.HasIndex(x => x.UserId);
        });
    }
}
