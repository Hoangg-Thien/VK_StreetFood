using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VK.Core.Entities;

namespace VK.Infrastructure.Configurations;

public class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("Tours");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.Emoji)
            .HasMaxLength(10);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasMany(t => t.TourPoints)
            .WithOne(tp => tp.Tour)
            .HasForeignKey(tp => tp.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Translations)
            .WithOne(tr => tr.Tour)
            .HasForeignKey(tr => tr.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.Name);
        builder.HasIndex(t => t.Status);
    }
}

public class TourTranslationConfiguration : IEntityTypeConfiguration<TourTranslation>
{
    public void Configure(EntityTypeBuilder<TourTranslation> builder)
    {
        builder.ToTable("TourTranslations");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.HasIndex(t => new { t.TourId, t.LanguageCode })
            .IsUnique();
    }
}

public class TourPointOfInterestConfiguration : IEntityTypeConfiguration<TourPointOfInterest>
{
    public void Configure(EntityTypeBuilder<TourPointOfInterest> builder)
    {
        builder.ToTable("TourPointsOfInterest");

        builder.HasKey(tp => tp.Id);

        builder.HasOne(tp => tp.Tour)
            .WithMany(t => t.TourPoints)
            .HasForeignKey(tp => tp.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tp => tp.PointOfInterest)
            .WithMany(p => p.TourPoints)
            .HasForeignKey(tp => tp.PointOfInterestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tp => new { tp.TourId, tp.PointOfInterestId })
            .IsUnique();

        builder.HasIndex(tp => new { tp.TourId, tp.SortOrder });
    }
}
