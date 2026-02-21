using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;

namespace VK.Infrastructure.Data;

public class VKStreetFoodDbContext : DbContext
{
    public VKStreetFoodDbContext(DbContextOptions<VKStreetFoodDbContext> options)
        : base(options)
    {
    }

    public DbSet<PointOfInterest> PointsOfInterest { get; set; }
    public DbSet<AudioContent> AudioContents { get; set; }
    public DbSet<Vendor> Vendors { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Tourist> Tourists { get; set; }
    public DbSet<VisitLog> VisitLogs { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Analytics> Analytics { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<OpeningHours> OpeningHours { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VKStreetFoodDbContext).Assembly);

        // Global query filters for soft delete
        modelBuilder.Entity<PointOfInterest>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<AudioContent>().HasQueryFilter(a => !a.IsDeleted);
        modelBuilder.Entity<Vendor>().HasQueryFilter(v => !v.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<Tourist>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<VisitLog>().HasQueryFilter(v => !v.IsDeleted);
        modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Tag>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<Analytics>().HasQueryFilter(a => !a.IsDeleted);
        modelBuilder.Entity<Rating>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Favorite>().HasQueryFilter(f => !f.IsDeleted);
        modelBuilder.Entity<OpeningHours>().HasQueryFilter(o => !o.IsDeleted);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }
    }
}