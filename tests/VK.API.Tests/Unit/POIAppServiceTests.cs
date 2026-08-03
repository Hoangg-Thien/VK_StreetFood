using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VK.API.Services.AppServices;
using VK.Contracts.Responses;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Shared.DTOs;

namespace VK.API.Tests.Unit;

public class POIAppServiceTests
{
    [Fact]
    public async Task GetAllPOIsAsync_ReturnsLocalizedData_FallbackToDefault()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        context.PointsOfInterest.Add(new PointOfInterest
        {
            Name = "Default Name",
            Description = "Default Desc",
            Latitude = 10.77,
            Longitude = 106.69,
            Address = "Address 1",
            IsActive = true,
            Translations = new List<PointOfInterestTranslation>
            {
                new PointOfInterestTranslation { LanguageCode = "en", Name = "English Name", Description = "English Desc" }
            }
        });
        await context.SaveChangesAsync();

        var listVi = await service.GetAllPOIsAsync(languageCode: "vi");
        Assert.NotNull(listVi);
        Assert.Equal("Default Name", listVi.Single().Name);

        var listEn = await service.GetAllPOIsAsync(languageCode: "en");
        Assert.NotNull(listEn);
        Assert.Equal("English Name", listEn.Single().Name);
    }

    [Fact]
    public async Task GetPagedPOIsAsync_ReturnsCorrectPageAndMetadata()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        for (int i = 1; i <= 15; i++)
        {
            context.PointsOfInterest.Add(new PointOfInterest
            {
                Name = $"POI {i}",
                Description = "Desc",
                Latitude = 10,
                Longitude = 106,
                Address = "Addr",
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var paged = await service.GetPagedPOIsAsync(pageNumber: 2, pageSize: 10, languageCode: "en");
        Assert.NotNull(paged);

        Assert.Equal(5, paged.Items.Count());
        Assert.Equal(15, paged.TotalCount);
        Assert.Equal(2, paged.TotalPages);
        Assert.True(paged.HasPrevious);
        Assert.False(paged.HasNext);
    }

    [Fact]
    public async Task GetNearbyPOIsAsync_FiltersByDistance()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        // Center: 10.0, 106.0
        context.PointsOfInterest.Add(new PointOfInterest { Name = "Close", Latitude = 10.005, Longitude = 106.005, IsActive = true });
        context.PointsOfInterest.Add(new PointOfInterest { Name = "Far", Latitude = 10.1, Longitude = 106.1, IsActive = true });
        await context.SaveChangesAsync();

        var response = await service.GetNearbyPOIsAsync(latitude: 10.0, longitude: 106.0, radiusKm: 2.0);
        Assert.NotNull(response);

        Assert.Single(response);
        Assert.Equal("Close", response.First().Name);
    }

    [Fact]
    public async Task GetAllPOIsAsync_FiltersByCategoryIdAndSearch()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var cat1 = new Category { Name = "Street Food", IsActive = true };
        var cat2 = new Category { Name = "Drinks", IsActive = true };
        context.Categories.AddRange(cat1, cat2);
        await context.SaveChangesAsync();

        context.PointsOfInterest.AddRange(
            new PointOfInterest { Name = "Banh Mi Huynh Hoa", CategoryId = cat1.Id, IsActive = true, Latitude = 10, Longitude = 106 },
            new PointOfInterest { Name = "Pho 24", CategoryId = cat1.Id, IsActive = true, Latitude = 10, Longitude = 106 },
            new PointOfInterest { Name = "Ca Phe Sua Da", CategoryId = cat2.Id, IsActive = true, Latitude = 10, Longitude = 106 }
        );
        await context.SaveChangesAsync();

        var cat1Filtered = await service.GetAllPOIsAsync(categoryId: cat1.Id);
        Assert.Equal(2, cat1Filtered.Count);

        var searchFiltered = await service.GetAllPOIsAsync(search: "Banh Mi");
        Assert.Single(searchFiltered);
        Assert.Equal("Banh Mi Huynh Hoa", searchFiltered.First().Name);
    }

    [Fact]
    public async Task GetPOIByIdAsync_ReturnsPoiDetail_WhenFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var cat = new Category { Name = "Street Food", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var poi = new PointOfInterest
        {
            Name = "Com Tam Cali",
            Description = "Broken rice Cali",
            Address = "District 1",
            Latitude = 10.77,
            Longitude = 106.69,
            CategoryId = cat.Id,
            IsActive = true,
            Translations = new List<PointOfInterestTranslation>
            {
                new PointOfInterestTranslation { LanguageCode = "en", Name = "Cali Broken Rice", Description = "English broken rice" }
            }
        };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        var result = await service.GetPOIByIdAsync(poi.Id, languageCode: "en");
        Assert.NotNull(result);
        Assert.Equal("Cali Broken Rice", result.Name);
        Assert.Equal("English broken rice", result.Description);
        Assert.Equal("Street Food", result.Category);
    }

    [Fact]
    public async Task GetPOIByIdAsync_ReturnsNull_WhenNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetPOIByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsAllCategories()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var cat = new Category { Name = "Street Food", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var categories = await service.GetCategoriesAsync();
        Assert.NotNull(categories);
        Assert.Single(categories);
        Assert.Equal("Street Food", categories.First().Name);
    }

    [Fact]
    public async Task POIAppService_MapsTriggerPriorityAndTriggerRadiusMeters_FromDatabaseEntity()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var poi = new PointOfInterest
        {
            Name = "Oc Oanh Test",
            Description = "Delicious snails",
            Latitude = 10.7608,
            Longitude = 106.7032,
            Address = "534 Vinh Khanh",
            IsActive = true
        };
        poi.SetTriggerProfile(85, 60.5);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        // Test GetAllPOIsAsync
        var allList = await service.GetAllPOIsAsync();
        var allItem = Assert.Single(allList);
        Assert.Equal(85, allItem.Priority);
        Assert.Equal(60.5, allItem.TriggerRadiusMeters);

        // Test GetNearbyPOIsAsync
        var nearbyList = await service.GetNearbyPOIsAsync(10.7608, 106.7032, 1.0);
        var nearbyItem = Assert.Single(nearbyList);
        Assert.Equal(85, nearbyItem.Priority);
        Assert.Equal(60.5, nearbyItem.TriggerRadiusMeters);

        // Test GetPOIByIdAsync
        var detail = await service.GetPOIByIdAsync(poi.Id);
        Assert.NotNull(detail);
        Assert.Equal(85, detail.Priority);
        Assert.Equal(60.5, detail.TriggerRadiusMeters);
    }

    private static POIAppService CreateService(VKStreetFoodDbContext context)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Scheme = "http";
        accessor.HttpContext.Request.Host = new HostString("localhost");

        return new POIAppService(
            new Repository<PointOfInterest>(context),
            new Repository<Category>(context),
            NullLogger<POIAppService>.Instance,
            accessor);
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new VKStreetFoodDbContext(options);
    }
}
