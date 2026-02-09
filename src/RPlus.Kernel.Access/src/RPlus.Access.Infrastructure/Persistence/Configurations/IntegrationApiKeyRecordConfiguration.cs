using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Access.Domain.Entities;

namespace RPlus.Access.Infrastructure.Persistence.Configurations;

public sealed class IntegrationApiKeyRecordConfiguration : IEntityTypeConfiguration<IntegrationApiKeyRecord>
{
    public void Configure(EntityTypeBuilder<IntegrationApiKeyRecord> builder)
    {
        builder.ToTable("integration_api_keys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ApplicationId).HasColumnName("application_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(x => x.Environment).HasColumnName("environment").IsRequired().HasMaxLength(16);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(32);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");

        builder.HasIndex(x => x.ApplicationId);

        builder.HasOne(x => x.Application)
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .HasConstraintName("fk_integration_api_keys_applications_application_id");
    }
}
