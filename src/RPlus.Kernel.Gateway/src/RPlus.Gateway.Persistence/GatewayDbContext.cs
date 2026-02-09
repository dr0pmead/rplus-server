using Microsoft.EntityFrameworkCore;
using RPlus.Gateway.Domain.Entities;

namespace RPlus.Gateway.Persistence;

public class GatewayDbContext : DbContext, IGatewayDbContext
{
    public DbSet<GatewayRoute> Routes { get; set; }
    public DbSet<GatewayCluster> Clusters { get; set; }
    public DbSet<AppRelease> AppReleases { get; set; }

    public GatewayDbContext(DbContextOptions<GatewayDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GatewayRoute>(b =>
        {
            b.ToTable("routes");
            b.HasKey(x => x.RouteId);
            b.HasOne(x => x.Cluster)
             .WithMany(x => x.Routes)
             .HasForeignKey(x => x.ClusterId);
        });

        modelBuilder.Entity<GatewayCluster>(b =>
        {
            b.ToTable("clusters");
            b.HasKey(x => x.ClusterId);
        });

        modelBuilder.Entity<AppRelease>(b =>
        {
            b.ToTable("app_releases");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.AppName).IsUnique();
        });
    }
}
