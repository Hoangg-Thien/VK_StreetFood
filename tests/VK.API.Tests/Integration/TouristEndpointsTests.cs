using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VK.API.Tests.Infrastructure;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Shared.DTOs;

namespace VK.API.Tests.Integration;

public class TouristEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TouristEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterTouristEndpoint_CreatesTouristRecord_AndReturnsToken()
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

        // Verify DB record was created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
        var tourist = await db.Tourists.SingleAsync();
        Assert.Equal("integration-device-001", tourist.DeviceId);
        Assert.Equal("en", tourist.PreferredLanguage);

        // Verify JWT token was returned in the response
        var dto = await response.Content.ReadFromJsonAsync<TouristDto>();
        Assert.NotNull(dto);
        Assert.False(string.IsNullOrWhiteSpace(dto.Token),
            "RegisterTourist should return a non-empty JWT token.");

        // Token must be a valid 3-part JWT
        var parts = dto.Token!.Split('.');
        Assert.Equal(3, parts.Length);
    }

    // ── Log Visit (authenticated) ─────────────────────────────────────────────

    [Fact]
    public async Task LogVisitEndpoint_PersistsVisitLog_WithValidToken()
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

        // Use token belonging to the correct tourist — should succeed
        var client = _factory.CreateAuthenticatedTouristClient(touristId);
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

    [Fact]
    public async Task LogVisitEndpoint_Returns401_WithNoToken()
    {
        await _factory.ResetDatabaseAsync();

        var client = _factory.CreateClient(); // no bearer token
        var response = await client.PostAsJsonAsync("/api/Tourist/99/visits", new
        {
            poiId = 1,
            triggerMethod = "manual"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── IDOR guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_Returns403_WhenTokenDoesNotMatchTouristId()
    {
        await _factory.ResetDatabaseAsync();

        int tourist1Id = 0;
        int tourist2Id = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var t1 = new Tourist { DeviceId = "device-idor-1", PreferredLanguage = "vi" };
            var t2 = new Tourist { DeviceId = "device-idor-2", PreferredLanguage = "vi" };
            db.Tourists.AddRange(t1, t2);
            await db.SaveChangesAsync();
            tourist1Id = t1.Id;
            tourist2Id = t2.Id;
        });

        // Client authenticated as tourist1, but requests tourist2's stats → IDOR
        var client = _factory.CreateAuthenticatedTouristClient(tourist1Id);
        var response = await client.GetAsync($"/api/Tourist/{tourist2Id}/stats");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_Returns200_WhenTokenMatchesTouristId()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist { DeviceId = "device-owner-1", PreferredLanguage = "vi" };
            db.Tourists.Add(tourist);
            await db.SaveChangesAsync();
            touristId = tourist.Id;
        });

        var client = _factory.CreateAuthenticatedTouristClient(touristId);
        var response = await client.GetAsync($"/api/Tourist/{touristId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_Returns200_WhenAdminAccessesAnyTouristId()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist { DeviceId = "device-admin-access", PreferredLanguage = "vi" };
            db.Tourists.Add(tourist);
            await db.SaveChangesAsync();
            touristId = tourist.Id;
        });

        // Admin token bypasses IDOR check
        var adminClient = _factory.CreateAuthenticatedAdminClient();
        var response = await adminClient.GetAsync($"/api/Tourist/{touristId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Validation: Register ──────────────────────────────────────────────────

    [Fact]
    public async Task RegisterTourist_Returns400_WhenDeviceIdIsMissing()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        // DeviceId is omitted — [Required] should reject this
        var response = await client.PostAsJsonAsync("/api/Tourist/register", new
        {
            preferredLanguage = "en"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTourist_Returns400_WhenLatitudeIsOutOfRange()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        // Latitude = 999 is impossible — [Range(-90, 90)] should reject this
        var response = await client.PostAsJsonAsync("/api/Tourist/register", new
        {
            deviceId = "device-bad-lat",
            latitude = 999.0,
            longitude = 106.69
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Validation: SubmitRating ──────────────────────────────────────────────

    [Fact]
    public async Task SubmitRating_Returns400_WhenScoreIsOutOfRange()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;
        int poiId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist { DeviceId = "device-rating-score", PreferredLanguage = "vi" };
            var poi = new PointOfInterest
            {
                Name = "Pho",
                Description = "Noodle soup",
                Latitude = 10.77,
                Longitude = 106.68,
                Address = "District 1",
                IsActive = true
            };
            db.Tourists.Add(tourist);
            db.PointsOfInterest.Add(poi);
            await db.SaveChangesAsync();
            touristId = tourist.Id;
            poiId = poi.Id;
        });

        var client = _factory.CreateAuthenticatedTouristClient(touristId);

        // Score = 9999 violates [Range(1, 5)]
        var response = await client.PostAsJsonAsync($"/api/Tourist/{touristId}/ratings", new
        {
            poiId,
            score = 9999,
            comment = "Invalid score"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitRating_Returns400_WhenPOIIdIsZero()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist { DeviceId = "device-rating-poiid", PreferredLanguage = "vi" };
            db.Tourists.Add(tourist);
            await db.SaveChangesAsync();
            touristId = tourist.Id;
        });

        var client = _factory.CreateAuthenticatedTouristClient(touristId);

        // POIId = 0 violates [Range(1, int.MaxValue)]
        var response = await client.PostAsJsonAsync($"/api/Tourist/{touristId}/ratings", new
        {
            poiId = 0,
            score = 3
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Validation: LogVisit ──────────────────────────────────────────────────

    [Fact]
    public async Task LogVisit_Returns400_WhenBothPoiIdsAreZeroAndLatitudeInvalid()
    {
        await _factory.ResetDatabaseAsync();

        int touristId = 0;

        await _factory.ExecuteDbContextAsync(async db =>
        {
            var tourist = new Tourist { DeviceId = "device-logvisit-bad", PreferredLanguage = "vi" };
            db.Tourists.Add(tourist);
            await db.SaveChangesAsync();
            touristId = tourist.Id;
        });

        var client = _factory.CreateAuthenticatedTouristClient(touristId);

        // Latitude = -999 violates [Range(-90, 90)]
        var response = await client.PostAsJsonAsync($"/api/Tourist/{touristId}/visits", new
        {
            poiId = 1,
            latitude = -999.0,
            longitude = 106.68
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

