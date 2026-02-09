using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Runtime.Domain.Entities;

namespace RPlus.Kernel.Runtime.Persistence;

public sealed class RuntimeDbContext : DbContext
{
    public RuntimeDbContext(DbContextOptions<RuntimeDbContext> options)
        : base(options)
    {
    }

    public DbSet<RuntimeGraphExecution> GraphExecutions => Set<RuntimeGraphExecution>();
    public DbSet<RuntimeGraphNodeState> GraphNodeStates => Set<RuntimeGraphNodeState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("runtime");

        modelBuilder.Entity<RuntimeGraphExecution>(b =>
        {
            b.ToTable("graph_executions");
            b.HasKey(x => x.Id);
            b.Property(x => x.OperationId).HasMaxLength(128);
            b.Property(x => x.ActionsJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.RuleId, x.UserId, x.OperationId }).IsUnique();
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<RuntimeGraphNodeState>(b =>
        {
            b.ToTable("graph_node_states");
            b.HasKey(x => new { x.RuleId, x.UserId, x.NodeId });
            b.Property(x => x.NodeId).HasMaxLength(64);
            b.Property(x => x.StateJson).HasColumnType("jsonb");
        });
    }
}
