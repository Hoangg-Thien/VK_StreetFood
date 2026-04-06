using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VK.Core.Entities;

namespace VK.Infrastructure.Configurations;

public class PointOfInterestTranslationConfiguration : IEntityTypeConfiguration<PointOfInterestTranslation>
{
    public void Configure(EntityTypeBuilder<PointOfInterestTranslation> builder)
    {
        builder.ToTable("PointOfInterestTranslations");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.Address)
            .HasMaxLength(500);

        builder.HasIndex(t => new { t.PointOfInterestId, t.LanguageCode })
            .IsUnique();
    }
}
