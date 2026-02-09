using Microsoft.EntityFrameworkCore;
using RPlus.Organization.Application.Interfaces;
using RPlus.Organization.Domain.Entities;

namespace RPlus.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options), IOrganizationDbContext
{
    public DbSet<OrgNode> OrgNodes => Set<OrgNode>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<UserAssignment> UserAssignments => Set<UserAssignment>();
    public DbSet<UserRoleOverride> UserRoleOverrides => Set<UserRoleOverride>();
    public DbSet<NodeContext> NodeContexts => Set<NodeContext>();
    public DbSet<PositionContext> PositionContexts => Set<PositionContext>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("organization");

        modelBuilder.Entity<OrgNode>(entity =>
        {
            entity.ToTable("org_nodes");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Type).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Path).IsRequired().HasMaxLength(2048);
            entity.Property(x => x.Attributes).HasColumnType("jsonb");

            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.TenantId, x.Path }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ParentId });
            entity.HasIndex(x => new { x.TenantId, x.IsDeleted, x.ValidTo });
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.ToTable("positions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Attributes).HasColumnType("jsonb");

            entity.HasOne(x => x.Node)
                .WithMany(x => x.Positions)
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ReportsToPosition)
                .WithMany(x => x.DirectReports)
                .HasForeignKey(x => x.ReportsToPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.TenantId, x.NodeId, x.IsDeleted });
            entity.HasIndex(x => new { x.TenantId, x.ReportsToPositionId });
        });

        modelBuilder.Entity<UserAssignment>(entity =>
        {
            entity.ToTable("user_assignments");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Role).IsRequired().HasMaxLength(32);
            entity.Property(x => x.Type).IsRequired().HasMaxLength(32);

            entity.HasOne(x => x.Position)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Node)
                .WithMany()
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReplacementForAssignment)
                .WithMany()
                .HasForeignKey(x => x.ReplacementForAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.TenantId, x.UserId, x.IsDeleted, x.ValidTo });
            entity.HasIndex(x => new { x.TenantId, x.NodeId, x.IsDeleted, x.ValidTo });
            entity.HasIndex(x => new { x.TenantId, x.PositionId, x.IsDeleted, x.ValidTo });
        });

        modelBuilder.Entity<UserRoleOverride>(entity =>
        {
            entity.ToTable("user_role_overrides");
            entity.HasKey(x => new { x.AssignmentId, x.RoleCode });

            entity.Property(x => x.RoleCode).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Reason).HasMaxLength(512);

            entity.HasOne(x => x.Assignment)
                .WithMany(x => x.RoleOverrides)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NodeContext>(entity =>
        {
            entity.ToTable("node_contexts");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ResourceType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Data).HasColumnType("jsonb");
            entity.Property(x => x.InheritanceStrategy).IsRequired().HasMaxLength(32);

            entity.HasOne(x => x.Node)
                .WithMany(x => x.Contexts)
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.TenantId, x.NodeId, x.ResourceType, x.ValidTo });
        });

        modelBuilder.Entity<PositionContext>(entity =>
        {
            entity.ToTable("position_contexts");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ResourceType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Data).HasColumnType("jsonb");
            entity.Property(x => x.InheritanceStrategy).IsRequired().HasMaxLength(32);

            entity.HasOne(x => x.Position)
                .WithMany(x => x.Contexts)
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.TenantId, x.PositionId, x.ResourceType, x.ValidTo });
        });
    }
}

