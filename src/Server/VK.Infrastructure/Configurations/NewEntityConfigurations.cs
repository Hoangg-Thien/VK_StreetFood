using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VK.Core.Entities;

namespace VK.Infrastructure.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => t.Name).IsUnique();
    }
}

public class AnalyticsConfiguration : IEntityTypeConfiguration<Analytics>
{
    public void Configure(EntityTypeBuilder<Analytics> builder)
    {
        builder.ToTable("Analytics");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(a => new { a.PointOfInterestId, a.EventTimestamp });
        builder.HasIndex(a => a.TouristId);
    }
}

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Score)
            .IsRequired();

        builder.HasIndex(r => new { r.PointOfInterestId, r.TouristId });
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne(u => u.Vendor)
            .WithMany(v => v.Users)
            .HasForeignKey(u => u.VendorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("Favorites");
        builder.HasKey(f => f.Id);

        builder.HasIndex(f => new { f.TouristId, f.PointOfInterestId }).IsUnique();

        builder.HasOne(f => f.Tourist)
            .WithMany(t => t.Favorites)
            .HasForeignKey(f => f.TouristId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.PointOfInterest)
            .WithMany(p => p.Favorites)
            .HasForeignKey(f => f.PointOfInterestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OpeningHoursConfiguration : IEntityTypeConfiguration<OpeningHours>
{
    public void Configure(EntityTypeBuilder<OpeningHours> builder)
    {
        builder.ToTable("OpeningHours");
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => new { o.VendorId, o.DayOfWeek }).IsUnique();

        builder.HasOne(o => o.Vendor)
            .WithMany(v => v.OpeningHours)
            .HasForeignKey(o => o.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PoiOwnerRegistrationConfiguration : IEntityTypeConfiguration<PoiOwnerRegistration>
{
    public void Configure(EntityTypeBuilder<PoiOwnerRegistration> builder)
    {
        builder.ToTable("PoiOwnerRegistrations");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ShopName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.ShopAddress)
            .HasMaxLength(500);

        builder.Property(r => r.ContactPhone)
            .HasMaxLength(15);

        builder.Property(r => r.Notes)
            .HasMaxLength(1000);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.ReviewNote)
            .HasMaxLength(500);

        builder.HasOne(r => r.User)
            .WithMany(u => u.OwnerRegistrations)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReviewedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.PointOfInterest)
            .WithMany()
            .HasForeignKey(r => r.PointOfInterestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Vendor)
            .WithMany()
            .HasForeignKey(r => r.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.VendorId);
    }
}

public class PoiContentChangeRequestConfiguration : IEntityTypeConfiguration<PoiContentChangeRequest>
{
    public void Configure(EntityTypeBuilder<PoiContentChangeRequest> builder)
    {
        builder.ToTable("PoiContentChangeRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.ActionType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(r => r.TextContent)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.ReviewNote)
            .HasMaxLength(500);

        builder.HasOne(r => r.OwnerUser)
            .WithMany()
            .HasForeignKey(r => r.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReviewedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Vendor)
            .WithMany()
            .HasForeignKey(r => r.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.PointOfInterest)
            .WithMany()
            .HasForeignKey(r => r.PointOfInterestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.AudioContent)
            .WithMany()
            .HasForeignKey(r => r.AudioContentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.OwnerUserId);
        builder.HasIndex(r => r.PointOfInterestId);
    }
}

public class QrPaymentConfigConfiguration : IEntityTypeConfiguration<QrPaymentConfig>
{
    public void Configure(EntityTypeBuilder<QrPaymentConfig> builder)
    {
        builder.ToTable("QrPaymentConfigs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.DefaultAmountVnd)
            .HasPrecision(12, 0)
            .HasDefaultValue(0);

        builder.Property(c => c.DeepLinkName)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("pay");

        builder.Property(c => c.QrTtlMinutes)
            .IsRequired()
            .HasDefaultValue(15);
    }
}