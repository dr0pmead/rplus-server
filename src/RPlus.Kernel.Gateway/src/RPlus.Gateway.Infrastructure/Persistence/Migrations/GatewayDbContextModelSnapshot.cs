using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Gateway.Persistence;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Gateway.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GatewayDbContext))]
internal class GatewayDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.1")
                    .HasAnnotation("Relational:MaxIdentifierLength", 63);
        modelBuilder.UseIdentityByDefaultColumns();

        modelBuilder.Entity("RPlus.Gateway.Domain.Entities.AppRelease", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("AppName").IsRequired().HasColumnType("text");
            b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("DisplayName").IsRequired().HasColumnType("text");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.Property<int>("LatestVersionCode").HasColumnType("integer");
            b.Property<string>("Message").HasColumnType("text");
            b.Property<int>("MinVersionCode").HasColumnType("integer");
            b.Property<Dictionary<string, string>>("StoreUrls").IsRequired().HasColumnType("jsonb");
            b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("AppName").IsUnique();
            b.ToTable("app_releases");
        });

        modelBuilder.Entity("RPlus.Gateway.Domain.Entities.GatewayCluster", b =>
        {
            b.Property<string>("ClusterId").HasColumnType("text");
            b.Property<string>("Address").IsRequired().HasColumnType("text");
            b.Property<string>("HealthCheckPath").HasColumnType("text");
            b.Property<string>("LoadBalancingPolicy").IsRequired().HasColumnType("text");
            b.HasKey("ClusterId");
            b.ToTable("clusters");
        });

        modelBuilder.Entity("RPlus.Gateway.Domain.Entities.GatewayRoute", b =>
        {
            b.Property<string>("RouteId").HasColumnType("text");
            b.Property<string>("AccessPolicy").HasColumnType("text");
            b.Property<string>("AuthPolicy").IsRequired().HasColumnType("text");
            b.Property<string>("ClusterId").IsRequired().HasColumnType("text");
            b.Property<bool>("IsEnabled").HasColumnType("boolean");
            b.Property<string[]>("Methods").IsRequired().HasColumnType("text[]");
            b.Property<string>("PathPattern").IsRequired().HasColumnType("text");
            b.Property<int>("Priority").HasColumnType("integer");
            b.HasKey("RouteId");
            b.HasIndex("ClusterId");
            b.ToTable("routes");
        });

        modelBuilder.Entity("RPlus.Gateway.Domain.Entities.GatewayRoute", b =>
        {
            b.HasOne("RPlus.Gateway.Domain.Entities.GatewayCluster", "Cluster")
             .WithMany("Routes")
             .HasForeignKey("ClusterId")
             .OnDelete(DeleteBehavior.Cascade)
             .IsRequired();
            b.Navigation("Cluster");
        });

        modelBuilder.Entity("RPlus.Gateway.Domain.Entities.GatewayCluster", b => b.Navigation("Routes"));
    }
}
