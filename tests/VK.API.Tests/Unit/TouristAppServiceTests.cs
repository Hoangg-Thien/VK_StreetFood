using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VK.API.Auth;
using VK.API.Common;
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

        var dto = await service.RegisterTouristAsync(request);

        Assert.Equal("device-new-001", dto.DeviceId);
        Assert.Equal("en", dto.PreferredLanguage);
        Assert.Equal(1, await context.Tourists.CountAsync());

        // JWT token must be returned on first registration
        Assert.False(string.IsNullOrWhiteSpace(dto.Token),
            "RegisterTourist should return a JWT token for the new tourist.");
        Assert.Equal(3, dto.Token!.Split('.').Length); // valid 3-part JWT
    }

    [Fact]
    public async Task RegisterTouristAsync_ReturnsToken_WhenTouristAlreadyExists()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var request = new RegisterTouristRequest { DeviceId = "device-existing-001" };

        // First call — creates the tourist
        await service.RegisterTouristAsync(request);

        // Second call — tourist already exists; should still return a token (re-login)
        var dto = await service.RegisterTouristAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(dto.Token),
            "RegisterTourist should return a JWT token even for an existing device.");
        Assert.Equal(1, await context.Tourists.CountAsync()); // no duplicates
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

        var res1 = await service.LogVisitAsync(tourist.Id, request);
        var res2 = await service.LogVisitAsync(tourist.Id, request);

        Assert.Equal(ServiceResultStatus.Success, res1.Status);
        Assert.Equal(ServiceResultStatus.Success, res2.Status);
        Assert.Equal(1, await context.VisitLogs.CountAsync());
        Assert.Equal(1, (await context.Tourists.SingleAsync()).TotalVisits);
    }

    [Fact]
    public async Task LogVisitAsync_ReturnsBadRequest_WhenPoiIdInvalid()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.LogVisitAsync(1, new LogVisitRequest { POIId = 0 });
        Assert.Equal(ServiceResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task LogVisitAsync_ReturnsNotFound_WhenTouristOrPoiDoesNotExist()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result1 = await service.LogVisitAsync(999, new LogVisitRequest { POIId = 1 });
        Assert.Equal(ServiceResultStatus.NotFound, result1.Status);

        var tourist = new Tourist { DeviceId = "dev-1" };
        context.Tourists.Add(tourist);
        await context.SaveChangesAsync();

        var result2 = await service.LogVisitAsync(tourist.Id, new LogVisitRequest { POIId = 999 });
        Assert.Equal(ServiceResultStatus.NotFound, result2.Status);
    }

    [Fact]
    public async Task UpdateLocationAsync_ReturnsNotFound_WhenTouristDoesNotExist()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.UpdateLocationAsync(999, new UpdateLocationRequest { Latitude = 10.0, Longitude = 106.0 });
        Assert.Equal(ServiceResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SubmitRatingAsync_ReturnsBadRequest_WhenScoreOutOfRange()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.SubmitRatingAsync(1, new SubmitRatingRequest { POIId = 1, Score = 6 });
        Assert.Equal(ServiceResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsNull_WhenTouristDoesNotExist()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetStatsAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLocationAsync_Success_UpdatesCoordinatesAndReturnsNearbyPois()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var tourist = new Tourist { DeviceId = "dev-loc-01" };
        var poi = new PointOfInterest { Name = "Nearby Stall", Latitude = 10.001, Longitude = 106.001, IsActive = true };
        context.Tourists.Add(tourist);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        var result = await service.UpdateLocationAsync(tourist.Id, new UpdateLocationRequest { Latitude = 10.0, Longitude = 106.0 });

        Assert.Equal(ServiceResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.NearbyPOIs);
        var updatedTourist = await context.Tourists.FindAsync(tourist.Id);
        Assert.Equal(10.0, updatedTourist!.LastLatitude);
        Assert.Equal(106.0, updatedTourist.LastLongitude);
    }

    [Fact]
    public async Task GetVisitHistoryAsync_ReturnsVisitHistoryList()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var tourist = new Tourist { DeviceId = "dev-hist-01" };
        var poi = new PointOfInterest { Name = "Bun Bo Hue", Latitude = 10, Longitude = 106, IsActive = true };
        context.Tourists.Add(tourist);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        context.VisitLogs.Add(new VisitLog
        {
            TouristId = tourist.Id,
            PointOfInterestId = poi.Id,
            VisitedAt = DateTime.UtcNow,
            LanguageUsed = "vi"
        });
        await context.SaveChangesAsync();

        var history = await service.GetVisitHistoryAsync(tourist.Id);
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal("Bun Bo Hue", history[0].POIName);
    }

    [Fact]
    public async Task AddAndRemoveFavoriteAsync_WorksCorrectly()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var tourist = new Tourist { DeviceId = "dev-fav-01" };
        var poi = new PointOfInterest { Name = "Goi Cuon", Latitude = 10, Longitude = 106, IsActive = true };
        context.Tourists.Add(tourist);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        // 1. Add favorite
        var addRes = await service.AddFavoriteAsync(tourist.Id, new AddFavoriteRequest { POIId = poi.Id });
        Assert.Equal(ServiceResultStatus.Success, addRes.Status);
        Assert.Equal(1, await context.Favorites.CountAsync(f => !f.IsDeleted));

        // 2. Get favorites
        var favs = await service.GetFavoritesAsync(tourist.Id);
        Assert.Single(favs);
        Assert.Equal("Goi Cuon", favs[0].Name);

        // 3. Remove favorite
        var remRes = await service.RemoveFavoriteAsync(tourist.Id, poi.Id);
        Assert.Equal(ServiceResultStatus.Success, remRes.Status);
        Assert.Equal(0, await context.Favorites.CountAsync(f => !f.IsDeleted));
    }

    [Fact]
    public async Task SubmitRatingAsync_CalculatesAverageAndTotalRatings()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var tourist1 = new Tourist { DeviceId = "dev-rate-01" };
        var tourist2 = new Tourist { DeviceId = "dev-rate-02" };
        var poi = new PointOfInterest { Name = "Hu Tieu", Latitude = 10, Longitude = 106, IsActive = true };
        context.Tourists.AddRange(tourist1, tourist2);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        var res1 = await service.SubmitRatingAsync(tourist1.Id, new SubmitRatingRequest { POIId = poi.Id, Score = 5, Comment = "Tuyet voi" });
        Assert.Equal(ServiceResultStatus.Success, res1.Status);

        var res2 = await service.SubmitRatingAsync(tourist2.Id, new SubmitRatingRequest { POIId = poi.Id, Score = 3, Comment = "Binh thuong" });
        Assert.Equal(ServiceResultStatus.Success, res2.Status);

        var updatedPoi = await context.PointsOfInterest.FindAsync(poi.Id);
        Assert.Equal(2, updatedPoi!.TotalRatings);
        Assert.Equal(4.0m, updatedPoi.AverageRating);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCalculatedStats_WhenTouristExists()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var tourist = new Tourist { DeviceId = "dev-stats-01", TotalVisits = 5 };
        var poi = new PointOfInterest { Name = "Che Ba Mau", Latitude = 10, Longitude = 106, IsActive = true };
        context.Tourists.Add(tourist);
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        context.Analytics.AddRange(
            new Analytics
            {
                TouristId = tourist.Id,
                PointOfInterestId = poi.Id,
                EventType = "audio_play",
                DurationSeconds = 0
            },
            new Analytics
            {
                TouristId = tourist.Id,
                PointOfInterestId = poi.Id,
                EventType = "audio_complete",
                DurationSeconds = 120
            }
        );
        await context.SaveChangesAsync();

        var stats = await service.GetStatsAsync(tourist.Id);
        Assert.NotNull(stats);
        Assert.Equal(5, stats.TotalVisits);
        Assert.Equal(1, stats.TotalAudioPlays);
        Assert.Equal(2.0, stats.TotalAudioMinutes);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TouristAppService CreateService(VKStreetFoodDbContext context)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        accessor.HttpContext.Request.Scheme = "http";
        accessor.HttpContext.Request.Host = new HostString("localhost:5201");

        // Build a real JwtTokenService with an in-test config (same key as integration tests)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-for-unit-tests-that-is-long-enough-for-hmac",
                ["Jwt:Issuer"] = "VKStreetFoodAPI",
                ["Jwt:Audience"] = "VKStreetFoodClients",
                ["Jwt:ExpiryDays"] = "365"
            })
            .Build();

        var tokenService = new JwtTokenService(config);

        return new TouristAppService(
            new Repository<Tourist>(context),
            new Repository<PointOfInterest>(context),
            new Repository<VisitLog>(context),
            new Repository<Favorite>(context),
            new Repository<Rating>(context),
            new Repository<Analytics>(context),
            new UnitOfWork(context),
            NullLogger<TouristAppService>.Instance,
            accessor,
            tokenService);
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new VKStreetFoodDbContext(options);
    }
}
