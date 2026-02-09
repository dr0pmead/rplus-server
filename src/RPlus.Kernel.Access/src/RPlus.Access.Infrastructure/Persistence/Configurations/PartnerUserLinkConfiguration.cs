using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPlus.Access.Domain.Entities;

namespace RPlus.Access.Infrastructure.Persistence.Configurations;

public sealed class PartnerUserLinkConfiguration : IEntityTypeConfiguration<PartnerUserLink>
{
    public void Configure(EntityTypeBuilder<PartnerUserLink> builder)
    {
        builder.ToTable("partner_user_links");

        builder.HasKey(x => new { x.ApplicationId, x.UserId });

        builder.Property(x => x.ApplicationId).HasColumnName("application_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.UserId);
    }
}

