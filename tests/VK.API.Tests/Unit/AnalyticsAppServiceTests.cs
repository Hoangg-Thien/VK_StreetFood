using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VK.API.Models;
using VK.API.Services.AppServices;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;

namespace VK.API.Tests.Unit;

public class AnalyticsAppServiceTests
{
    [Fact]
    public async Task RecordEventAsync_AudioPlay_UpdatesLatestVisitAndNormalizesEventType()
    {
        using var context = CreateContext();

        var poi = new PointOfInterest
        {
            Name = "Pho Stall",
            Description = "Signature pho",
            Latitude = 10.0,
            Longitude = 106.0,
            Address = "Ho Chi Minh City",
            IsActive = true
        };

        var tourist = new Tourist
        {
            DeviceId = "device-analytics-001",
            PreferredLanguage = "vi"
        };

        context.PointsOfInterest.Add(poi);
        context.Tourists.Add(tourist);
        await context.SaveChangesAsync();

        context.VisitLogs.Add(new VisitLog
        {
            TouristId = tourist.Id,
            PointOfInterestId = poi.Id,
            VisitedAt = DateTime.UtcNow.AddHours(-1),
            LanguageUsed = string.Empty,
            VisitorLatitude = 10.0,
            VisitorLongitude = 106.0
        });
        await context.SaveChangesAsync();

        var service = new AnalyticsAppService(
            new Repository<Analytics>(context),
            new Repository<VisitLog>(context),
            new Repository<Rating>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Tourist>(context),
            new UnitOfWork(context),
            NullLogger<AnalyticsAppService>.Instance);

        var request = new RecordEventRequest
        {
            TouristId = tourist.Id,
            POIId = poi.Id,
            EventType = " AUDIO_PLAY ",
            LanguageCode = "en",
            DurationSeconds = 42
        };

        var result = await service.RecordEventAsync(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var analyticsEvent = await context.Analytics.SingleAsync();
        Assert.Equal("audio_play", analyticsEvent.EventType);

        var visit = await context.VisitLogs.SingleAsync();
        Assert.True(visit.AudioPlayed);
        Assert.Equal("en", visit.LanguageUsed);
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new VKStreetFoodDbContext(options);
    }
}
