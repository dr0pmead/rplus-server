using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Users.Domain.Entities;
using RPlus.SDK.Users.Enums;

#nullable enable
namespace RPlus.Users.Infrastructure.Persistence;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
  public DbSet<UserEntity> Users => this.Set<UserEntity>();

  public DbSet<OutboxMessageEntity> OutboxMessages => this.Set<OutboxMessageEntity>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.HasDefaultSchema("users");
    modelBuilder.Entity<UserEntity>(entity =>
    {
      entity.ToTable("Users");
      entity.HasKey(x => x.Id);
      
      // FIO fields removed - now in HR.EmployeeProfile
      // entity.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
      // entity.Property(x => x.LastName).IsRequired().HasMaxLength(100);
      // entity.Property(x => x.MiddleName).HasMaxLength(100);
      
      entity.Property(x => x.PreferredName).HasMaxLength(100);
      entity.Property(x => x.Locale).IsRequired().HasMaxLength(10);
      entity.Property(x => x.TimeZone).IsRequired().HasMaxLength(50);
      entity.Property(x => x.PreferencesJson)
        .HasColumnType("jsonb")
        .HasDefaultValueSql("'{}'::jsonb");
      entity.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
      entity.Property(x => x.CreatedAt).IsRequired();
      entity.Property(x => x.UpdatedAt).IsRequired();
    });
    modelBuilder.Entity<OutboxMessageEntity>(entity =>
    {
      entity.ToTable("OutboxMessages");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Topic).IsRequired().HasMaxLength(255);
      entity.Property(x => x.EventType).IsRequired().HasMaxLength(255);
      entity.Property(x => x.Payload).IsRequired().HasColumnType("jsonb");
      entity.Property(x => x.AggregateId).IsRequired().HasMaxLength(128);
      entity.Property(x => x.Status).IsRequired().HasMaxLength(50);
      entity.Property(x => x.CreatedAt).IsRequired();
      entity.HasIndex(x => new { x.Status, x.CreatedAt });
    });
  }
}
