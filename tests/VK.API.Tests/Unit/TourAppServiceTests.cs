using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.API.Services.AppServices;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Shared.DTOs;

namespace VK.API.Tests.Unit;

public class TourAppServiceTests
{
    [Fact]
    public async Task GetToursAsync_ReturnsActiveAndInactiveTours_Localized()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        context.Tours.Add(new Tour
        {
            Name = "Default Tour",
            Status = "active",
            EstimatedDurationMinutes = 120,
            Translations = new List<TourTranslation>
            {
                new TourTranslation { LanguageCode = "en", Name = "English Tour", Description = "English Desc" }
            }
        });
        await context.SaveChangesAsync();

        // 1. Fallback (Vietnamese)
        var listVi = await service.GetToursAsync(languageCode: "vi");
        Assert.NotNull(listVi);
        Assert.Equal("Default Tour", listVi.Single().Name);

        // 2. Exact Match (English)
        var listEn = await service.GetToursAsync(languageCode: "en");
        Assert.NotNull(listEn);
        Assert.Equal("English Tour", listEn.Single().Name);
        Assert.Equal("English Desc", listEn.Single().Description);
    }

    [Fact]
    public async Task GetTourByIdAsync_ReturnsPointsInOrder()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var poi1 = new PointOfInterest { Name = "POI 1", IsActive = true };
        var poi2 = new PointOfInterest { Name = "POI 2", IsActive = true };

        var tour = new Tour
        {
            Name = "Ordered Tour",
            Status = "active",
            TourPoints = new List<TourPointOfInterest>
            {
                new TourPointOfInterest { PointOfInterest = poi2, SortOrder = 2 },
                new TourPointOfInterest { PointOfInterest = poi1, SortOrder = 1 }
            }
        };

        context.Tours.Add(tour);
        await context.SaveChangesAsync();

        var dto = await service.GetTourByIdAsync(tour.Id);
        Assert.NotNull(dto);

        Assert.Equal(2, dto.Points.Count);
        Assert.Equal("POI 1", dto.Points[0].Name); // SortOrder 1 comes first
        Assert.Equal("POI 2", dto.Points[1].Name);
    }

    [Fact]
    public async Task GetTourByIdAsync_ReturnsNull_WhenTourNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var dto = await service.GetTourByIdAsync(999);
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetTourByIdAsync_ReturnsLocalizedDetails()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var poi = new PointOfInterest
        {
            Name = "Default POI",
            IsActive = true,
            Translations = new List<PointOfInterestTranslation>
            {
                new PointOfInterestTranslation { LanguageCode = "en", Name = "English POI", Description = "English POI Desc" }
            }
        };

        var tour = new Tour
        {
            Name = "Default Tour",
            Description = "Default Tour Desc",
            Status = "active",
            Translations = new List<TourTranslation>
            {
                new TourTranslation { LanguageCode = "en", Name = "English Tour", Description = "English Tour Desc" }
            },
            TourPoints = new List<TourPointOfInterest>
            {
                new TourPointOfInterest { PointOfInterest = poi, SortOrder = 1 }
            }
        };

        context.Tours.Add(tour);
        await context.SaveChangesAsync();

        var dtoEn = await service.GetTourByIdAsync(tour.Id, languageCode: "en");
        Assert.NotNull(dtoEn);
        Assert.Equal("English Tour", dtoEn.Name);
        Assert.Equal("English Tour Desc", dtoEn.Description);
        Assert.Single(dtoEn.Points);
        Assert.Equal("English POI", dtoEn.Points[0].Name);
    }

    private static TourAppService CreateService(VKStreetFoodDbContext context)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Scheme = "http";
        accessor.HttpContext.Request.Host = new HostString("localhost");

        return new TourAppService(
            new Repository<Tour>(context),
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
