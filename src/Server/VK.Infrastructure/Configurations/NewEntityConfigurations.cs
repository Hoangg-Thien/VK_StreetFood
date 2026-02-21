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