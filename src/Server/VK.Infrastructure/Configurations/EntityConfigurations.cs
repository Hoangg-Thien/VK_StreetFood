using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VK.Core.Entities;

namespace VK.Infrastructure.Configurations;

public class AudioContentConfiguration : IEntityTypeConfiguration<AudioContent>
{
    public void Configure(EntityTypeBuilder<AudioContent> builder)
    {
        builder.ToTable("AudioContents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(a => a.TextContent)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(a => a.AudioFileUrl)
            .HasMaxLength(500);

        builder.HasIndex(a => new { a.PointOfInterestId, a.LanguageCode })
            .IsUnique();
    }
}

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(v => v.ContactPerson)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.PhoneNumber)
            .IsRequired()
            .HasMaxLength(15);

        builder.Property(v => v.Email)
            .HasMaxLength(200);

        builder.Property(v => v.AverageRating)
            .HasPrecision(3, 2);

        builder.HasMany(v => v.Products)
            .WithOne(p => p.Vendor)
            .HasForeignKey(p => p.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);
    }
}

public class TouristConfiguration : IEntityTypeConfiguration<Tourist>
{
    public void Configure(EntityTypeBuilder<Tourist> builder)
    {
        builder.ToTable("Tourists");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.DeviceId)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.DeviceId)
            .IsUnique();

        builder.Property(t => t.PreferredLanguage)
            .HasMaxLength(10);

        builder.Property(t => t.LastLatitude)
            .HasPrecision(10, 7);

        builder.Property(t => t.LastLongitude)
            .HasPrecision(10, 7);

        builder.HasMany(t => t.VisitLogs)
            .WithOne(v => v.Tourist)
            .HasForeignKey(v => v.TouristId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VisitLogConfiguration : IEntityTypeConfiguration<VisitLog>
{
    public void Configure(EntityTypeBuilder<VisitLog> builder)
    {
        builder.ToTable("VisitLogs");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VisitorLatitude)
            .HasPrecision(10, 7);

        builder.Property(v => v.VisitorLongitude)
            .HasPrecision(10, 7);

        builder.Property(v => v.LanguageUsed)
            .HasMaxLength(10);

        builder.HasOne(v => v.PointOfInterest)
            .WithMany()
            .HasForeignKey(v => v.PointOfInterestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}