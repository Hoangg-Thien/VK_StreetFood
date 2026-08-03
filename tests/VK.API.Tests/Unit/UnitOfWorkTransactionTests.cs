using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;

namespace VK.API.Tests.Unit;

public class UnitOfWorkTransactionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<VKStreetFoodDbContext> _options;

    public UnitOfWorkTransactionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new VKStreetFoodDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsChanges_WhenNoExceptionThrown()
    {
        using var context = new VKStreetFoodDbContext(_options);
        var uow = new UnitOfWork(context);

        await uow.ExecuteInTransactionAsync(async () =>
        {
            var category = new Category { Name = "Street Food", Description = "Vietnamese street food" };
            context.Categories.Add(category);
            await uow.SaveChangesAsync();

            var poi = new PointOfInterest
            {
                Name = "Pho Thin",
                Description = "Famous beef noodle soup",
                Address = "13 Lo Duc",
                Category = category,
                IsActive = true
            };
            context.PointsOfInterest.Add(poi);
            await uow.SaveChangesAsync();
        });

        // Verify with a new context that changes were persisted
        using var verifyContext = new VKStreetFoodDbContext(_options);
        var poiInDb = await verifyContext.PointsOfInterest.Include(p => p.Category).FirstOrDefaultAsync(p => p.Name == "Pho Thin");
        Assert.NotNull(poiInDb);
        Assert.NotNull(poiInDb.Category);
        Assert.Equal("Street Food", poiInDb.Category.Name);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackAllChanges_WhenExceptionOccurs()
    {
        using var context = new VKStreetFoodDbContext(_options);
        var uow = new UnitOfWork(context);

        // Pre-populate with 1 category
        var existingCategory = new Category { Name = "Existing Drinks", Description = "Drinks and beverages" };
        context.Categories.Add(existingCategory);
        await context.SaveChangesAsync();

        // Attempt transaction that adds a new category and POI, but throws midway
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await uow.ExecuteInTransactionAsync(async () =>
            {
                var newCategory = new Category { Name = "Desserts", Description = "Sweet dishes" };
                context.Categories.Add(newCategory);
                await uow.SaveChangesAsync();

                var poi = new PointOfInterest
                {
                    Name = "Che Ba Ba",
                    Description = "Sweet soup",
                    Address = "456 Market",
                    Category = newCategory,
                    IsActive = true
                };
                context.PointsOfInterest.Add(poi);
                await uow.SaveChangesAsync();

                // Simulate unexpected runtime failure
                throw new InvalidOperationException("Simulated unexpected failure during multi-write");
            });
        });

        // Verify with a new context that changes were rolled back completely
        using var verifyContext = new VKStreetFoodDbContext(_options);
        var dessertCategory = await verifyContext.Categories.FirstOrDefaultAsync(c => c.Name == "Desserts");
        var poiInDb = await verifyContext.PointsOfInterest.FirstOrDefaultAsync(p => p.Name == "Che Ba Ba");

        Assert.Null(dessertCategory);
        Assert.Null(poiInDb);

        // Verify existing category remains intact
        var existingCategoryInDb = await verifyContext.Categories.FirstOrDefaultAsync(c => c.Name == "Existing Drinks");
        Assert.NotNull(existingCategoryInDb);
    }
}
