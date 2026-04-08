using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VK.API.Tests.Infrastructure;
using VK.Core.Entities;
using VK.Infrastructure.Data;

namespace VK.API.Tests.Integration;

public class TouristEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TouristEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterTouristEndpoint_CreatesTouristRecord()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Tourist/register", new
        {
            deviceId = "integration-device-001",
            preferredLanguage = "en",
            latitude = 10.77,
            longitude = 106.69
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();

        var tourist = await db.Tourists.SingleAsync();
        Assert.Equal("integration-device-001", tourist.DeviceId);
        Assert.Equal("en", tourist.PreferredLanguage);
    }

    [Fact]
    public async Task LogVisitEndpoint_PersistsVisitLog()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;
        int poiId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist
            {
                DeviceId = "integration-device-visit",
                PreferredLanguage = "vi"
            };

            var poi = new PointOfInterest
            {
                Name = "Bun Bo",
                Description = "Hue noodles",
                Latitude = 10.78,
                Longitude = 106.68,
                Address = "District 3",
                IsActive = true
            };

            db.Tourists.Add(tourist);
            db.PointsOfInterest.Add(poi);
            await db.SaveChangesAsync();

            touristId = tourist.Id;
            poiId = poi.Id;
        });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/Tourist/{touristId}/visits", new
        {
            poiId,
            triggerMethod = "manual",
            latitude = 10.78,
            longitude = 106.68,
            languageCode = "vi"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var visits = await db.VisitLogs.Where(v => v.TouristId == touristId).ToListAsync();
            Assert.Single(visits);
            Assert.Equal(poiId, visits[0].PointOfInterestId);
        });
    }
}
