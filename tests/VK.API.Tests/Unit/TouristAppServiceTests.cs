using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VK.API.Services.AppServices;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Shared.DTOs;

namespace VK.API.Tests.Unit;

public class TouristAppServiceTests
{
    [Fact]
    public async Task RegisterTouristAsync_CreatesTourist_WhenDeviceDoesNotExist()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var request = new RegisterTouristRequest
        {
            DeviceId = "device-new-001",
            PreferredLanguage = "en",
            Latitude = 10.77,
            Longitude = 106.69
        };

        var result = await service.RegisterTouristAsync(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<TouristDto>(ok.Value);

        Assert.Equal("device-new-001", dto.DeviceId);
        Assert.Equal("en", dto.PreferredLanguage);
        Assert.Equal(1, await context.Tourists.CountAsync());
    }

    [Fact]
    public async Task LogVisitAsync_DeduplicatesVisitWithinFiveMinutes()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var tourist = new Tourist
        {
            DeviceId = "device-visit-001",
            PreferredLanguage = "vi",
            LastLatitude = 10.77,
            LastLongitude = 106.69
        };

        var poi = new PointOfInterest
        {
            Name = "Banh Mi",
            Description = "Street food spot",
            Latitude = 10.771,
            Longitude = 106.691,
            Address = "District 1",
            IsActive = true
        };

        context.Tourists.Add(tourist);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        var request = new LogVisitRequest
        {
            POIId = poi.Id,
            TriggerMethod = "geofence",
            Latitude = 10.77,
            Longitude = 106.69,
            LanguageCode = "vi"
        };

        await service.LogVisitAsync(tourist.Id, request);
        await service.LogVisitAsync(tourist.Id, request);

        Assert.Equal(1, await context.VisitLogs.CountAsync());
        Assert.Equal(1, (await context.Tourists.SingleAsync()).TotalVisits);
    }

    private static TouristAppService CreateService(VKStreetFoodDbContext context)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        accessor.HttpContext.Request.Scheme = "http";
        accessor.HttpContext.Request.Host = new HostString("localhost:5201");

        return new TouristAppService(
            new Repository<Tourist>(context),
            new Repository<PointOfInterest>(context),
            new Repository<VisitLog>(context),
            new Repository<Favorite>(context),
            new Repository<Rating>(context),
            new Repository<Analytics>(context),
            new UnitOfWork(context),
            NullLogger<TouristAppService>.Instance,
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
