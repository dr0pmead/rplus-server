using Microsoft.EntityFrameworkCore;
using RPlus.SDK.Infrastructure.Outbox;

namespace RPlus.Kernel.Guard.Infrastructure.Persistence;

public class GuardDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public GuardDbContext(DbContextOptions<GuardDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox");
            b.HasKey(x => x.Id);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}
