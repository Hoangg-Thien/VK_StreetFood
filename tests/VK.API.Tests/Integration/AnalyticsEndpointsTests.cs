using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VK.API.Tests.Infrastructure;
using VK.Core.Entities;

namespace VK.API.Tests.Integration;

public class AnalyticsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AnalyticsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RecordEventEndpoint_PersistsAnalyticsRow()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;
        int poiId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist
            {
                DeviceId = "integration-device-analytics",
                PreferredLanguage = "vi"
            };

            var poi = new PointOfInterest
            {
                Name = "Com Tam",
                Description = "Broken rice",
                Latitude = 10.75,
                Longitude = 106.67,
                Address = "District 1",
                IsActive = true
            };

            db.Tourists.Add(tourist);
            db.PointsOfInterest.Add(poi);
            await db.SaveChangesAsync();

            touristId = tourist.Id;
            poiId = poi.Id;
        });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Analytics/event", new
        {
            touristId,
            poiId,
            eventType = "audio_complete",
            languageCode = "vi",
            durationSeconds = 95
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var row = await db.Analytics.SingleAsync();
            Assert.Equal(poiId, row.PointOfInterestId);
            Assert.Equal(touristId, row.TouristId);
            Assert.Equal("audio_complete", row.EventType);
            Assert.Equal(95, row.DurationSeconds);
        });
    }
}
