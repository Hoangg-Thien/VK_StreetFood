using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VK.Core.Entities;

namespace VK.Infrastructure.Configurations;

public class PointOfInterestConfiguration : IEntityTypeConfiguration<PointOfInterest>
{
    public void Configure(EntityTypeBuilder<PointOfInterest> builder)
    {
        builder.ToTable("PointsOfInterest");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Latitude)
            .IsRequired()
            .HasPrecision(10, 7);

        builder.Property(p => p.Longitude)
            .IsRequired()
            .HasPrecision(10, 7);

        builder.Property(p => p.Address)
            .HasMaxLength(500);

        builder.Property(p => p.QRCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.AverageRating)
            .HasPrecision(3, 2);

        builder.HasIndex(p => p.QRCode)
            .IsUnique();

        builder.HasMany(p => p.AudioContents)
            .WithOne(a => a.PointOfInterest)
            .HasForeignKey(a => a.PointOfInterestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Vendors)
            .WithOne(v => v.PointOfInterest)
            .HasForeignKey(v => v.PointOfInterestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}