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

        var resultVi = await service.GetAllPOIsAsync(languageCode: "vi");
        var listVi = Assert.IsAssignableFrom<IEnumerable<POIListItemDto>>(((OkObjectResult)resultVi).Value);
        Assert.Equal("Default Name", listVi.Single().Name);

        var resultEn = await service.GetAllPOIsAsync(languageCode: "en");
        var listEn = Assert.IsAssignableFrom<IEnumerable<POIListItemDto>>(((OkObjectResult)resultEn).Value);
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

        var result = await service.GetPagedPOIsAsync(pageNumber: 2, pageSize: 10, languageCode: "en");
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResponse<POIListItemDto>>(ok.Value);

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

        var result = await service.GetNearbyPOIsAsync(latitude: 10.0, longitude: 106.0, radiusKm: 2.0);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IEnumerable<POIListItemDto>>(ok.Value);

        Assert.Single(response);
        Assert.Equal("Close", response.First().Name);
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
