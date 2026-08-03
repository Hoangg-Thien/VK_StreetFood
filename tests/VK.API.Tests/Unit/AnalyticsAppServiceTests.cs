using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VK.API.Common;
using VK.API.Models;
using VK.API.Services.AppServices;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Shared.DTOs;

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

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Success);
        Assert.True(result.Data.EventId > 0);

        var analyticsEvent = await context.Analytics.SingleAsync();
        Assert.Equal("audio_play", analyticsEvent.EventType);

        var visit = await context.VisitLogs.SingleAsync();
        Assert.True(visit.AudioPlayed);
        Assert.Equal("en", visit.LanguageUsed);
    }

    [Fact]
    public async Task GetPOISummaryAsync_ReturnsSummaryCorrectly()
    {
        using var context = CreateContext();

        var poi = new PointOfInterest
        {
            Name = "Com Tam",
            Description = "Broken rice",
            Latitude = 10.0,
            Longitude = 106.0,
            Address = "HCM",
            IsActive = true
        };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        context.Analytics.Add(new Analytics
        {
            PointOfInterestId = poi.Id,
            EventType = "view",
            CreatedAt = DateTime.UtcNow
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

        var result = await service.GetPOISummaryAsync(poi.Id, null, null);

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalViews);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsDashboardOverview()
    {
        using var context = CreateContext();

        var service = new AnalyticsAppService(
            new Repository<Analytics>(context),
            new Repository<VisitLog>(context),
            new Repository<Rating>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Tourist>(context),
            new UnitOfWork(context),
            NullLogger<AnalyticsAppService>.Instance);

        var result = await service.GetDashboardAsync(null, null);

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Overview);
    }

    [Fact]
    public async Task GetTopPOIsAsync_ReturnsOrderedTopPois()
    {
        using var context = CreateContext();

        var poi1 = new PointOfInterest { Name = "POI 1", IsActive = true, Latitude = 10, Longitude = 106 };
        var poi2 = new PointOfInterest { Name = "POI 2", IsActive = true, Latitude = 10, Longitude = 106 };
        context.PointsOfInterest.AddRange(poi1, poi2);
        await context.SaveChangesAsync();

        context.VisitLogs.AddRange(
            new VisitLog { PointOfInterestId = poi1.Id, VisitedAt = DateTime.UtcNow },
            new VisitLog { PointOfInterestId = poi1.Id, VisitedAt = DateTime.UtcNow },
            new VisitLog { PointOfInterestId = poi2.Id, VisitedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new AnalyticsAppService(
            new Repository<Analytics>(context),
            new Repository<VisitLog>(context),
            new Repository<Rating>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Tourist>(context),
            new UnitOfWork(context),
            NullLogger<AnalyticsAppService>.Instance);

        var result = await service.GetTopPOIsAsync(count: 5);

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("POI 1", result.Data[0].Name);
        Assert.Equal(2, result.Data[0].VisitCount);
    }

    [Fact]
    public async Task GetTopListenedPoisAsync_ReturnsTopListenedPois()
    {
        using var context = CreateContext();

        var poi = new PointOfInterest { Name = "POI Audio", IsActive = true, Latitude = 10, Longitude = 106 };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        context.Analytics.AddRange(
            new Analytics { PointOfInterestId = poi.Id, EventType = "audio_play", EventTimestamp = DateTime.UtcNow, TouristId = 1 },
            new Analytics { PointOfInterestId = poi.Id, EventType = "audio_complete", EventTimestamp = DateTime.UtcNow, TouristId = 1 }
        );
        await context.SaveChangesAsync();

        var service = new AnalyticsAppService(
            new Repository<Analytics>(context),
            new Repository<VisitLog>(context),
            new Repository<Rating>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Tourist>(context),
            new UnitOfWork(context),
            NullLogger<AnalyticsAppService>.Instance);

        var result = await service.GetTopListenedPoisAsync(null, null, null, null, 10);

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("POI Audio", result.Data[0].PoiName);
        Assert.Equal(1, result.Data[0].AudioPlayCount);
        Assert.Equal(1, result.Data[0].AudioCompleteCount);
        Assert.Equal(100.0, result.Data[0].CompletionRate);
    }

    [Fact]
    public async Task GetAverageListenPerPoiAsync_ReturnsAverageListen()
    {
        using var context = CreateContext();

        var poi = new PointOfInterest { Name = "POI Avg", IsActive = true, Latitude = 10, Longitude = 106 };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        context.Analytics.AddRange(
            new Analytics { PointOfInterestId = poi.Id, EventType = "audio_complete", DurationSeconds = 60, EventTimestamp = DateTime.UtcNow },
            new Analytics { PointOfInterestId = poi.Id, EventType = "audio_complete", DurationSeconds = 120, EventTimestamp = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new AnalyticsAppService(
            new Repository<Analytics>(context),
            new Repository<VisitLog>(context),
            new Repository<Rating>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Tourist>(context),
            new UnitOfWork(context),
            NullLogger<AnalyticsAppService>.Instance);

        var result = await service.GetAverageListenPerPoiAsync(null, null, null, null, 10);

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(90.0, result.Data[0].AverageDurationSeconds);
        Assert.Equal(2, result.Data[0].SampleCount);
    }

    [Fact]
    public async Task GetHeatmapAsync_And_GetAnonymousRoutesAsync_ReturnCorrectData()
    {
        using var context = CreateContext();

        var tourist = new Tourist
        {
            DeviceId = "dev-heat-01",
            LastLatitude = 10.7712,
            LastLongitude = 106.6912,
            LastLocationUpdate = DateTime.UtcNow,
            PreferredLanguage = "vi"
        };
        context.Tourists.Add(tourist);
        await context.SaveChangesAsync();

        var service = new AnalyticsAppService(
            new Repository<Analytics>(context),
            new Repository<VisitLog>(context),
            new Repository<Rating>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Tourist>(context),
            new UnitOfWork(context),
            NullLogger<AnalyticsAppService>.Instance);

        // Test Heatmap
        var heatmapResult = await service.GetHeatmapAsync(null, null, "vi", null);
        Assert.Equal(ServiceResultStatus.Success, heatmapResult.Status);
        Assert.NotNull(heatmapResult.Data);
        Assert.Single(heatmapResult.Data);
        Assert.Equal(1, heatmapResult.Data[0].Weight);

        // Test Routes
        var routeResult = await service.GetAnonymousRoutesAsync(null, null, "vi", null, 10);
        Assert.Equal(ServiceResultStatus.Success, routeResult.Status);
        Assert.NotNull(routeResult.Data);
        Assert.Single(routeResult.Data);
        Assert.StartsWith("anon-", routeResult.Data[0].AnonymousVisitorId);
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new VKStreetFoodDbContext(options);
    }
}
