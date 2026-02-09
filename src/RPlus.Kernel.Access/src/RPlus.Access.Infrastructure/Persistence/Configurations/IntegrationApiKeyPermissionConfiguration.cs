using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Access.Domain.Entities;

namespace RPlus.Access.Infrastructure.Persistence.Configurations;

public sealed class IntegrationApiKeyPermissionConfiguration : IEntityTypeConfiguration<IntegrationApiKeyPermission>
{
    public void Configure(EntityTypeBuilder<IntegrationApiKeyPermission> builder)
    {
        builder.ToTable("integration_api_key_permissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ApiKeyId).HasColumnName("api_key_id");
        builder.Property(x => x.PermissionId).HasColumnName("permission_id");
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at");

        builder.HasIndex(x => new { x.ApiKeyId, x.PermissionId }).IsUnique();
    }
}

