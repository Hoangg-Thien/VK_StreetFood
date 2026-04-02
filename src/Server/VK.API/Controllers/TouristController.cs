using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TouristController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<TouristController> _logger;

    public TouristController(VKStreetFoodDbContext context, ILogger<TouristController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Register or get tourist by device ID
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<TouristDto>> RegisterTourist([FromBody] RegisterTouristRequest request)
    {
        // Check if tourist already exists
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(t => t.DeviceId == request.DeviceId && !t.IsDeleted);

        if (tourist == null)
        {
            // Create new tourist
            tourist = new Tourist
            {
                DeviceId = request.DeviceId,
                PreferredLanguage = request.PreferredLanguage ?? "vi",
                LastLatitude = request.Latitude,
                LastLongitude = request.Longitude
            };

            _context.Tourists.Add(tourist);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New tourist registered with DeviceId: {DeviceId}", request.DeviceId);
        }
        else
        {
            // Update existing tourist
            tourist.PreferredLanguage = request.PreferredLanguage ?? tourist.PreferredLanguage;
            tourist.LastLatitude = request.Latitude ?? tourist.LastLatitude;
            tourist.LastLongitude = request.Longitude ?? tourist.LastLongitude;
            await _context.SaveChangesAsync();
        }

        return Ok(new TouristDto
        {
            TouristId = tourist.Id,
            DeviceId = tourist.DeviceId,
            PreferredLanguage = tourist.PreferredLanguage,
            TotalVisits = tourist.TotalVisits
        });
    }

    /// <summary>
    /// Update tourist GPS location (for background tracking)
    /// </summary>
    [HttpPut("{touristId}/location")]
    public async Task<ActionResult> UpdateLocation(int touristId, [FromBody] UpdateLocationRequest request)
    {
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
        {
            return NotFound(new { message = "Tourist không tồn tại" });
        }

        tourist.LastLatitude = request.Latitude;
        tourist.LastLongitude = request.Longitude;

        await _context.SaveChangesAsync();

        // Check for nearby POIs (geofencing logic)
        var nearbyPOIs = await CheckNearbyPOIs(request.Latitude, request.Longitude);

        return Ok(new
        {
            success = true,
            nearbyPOIs = nearbyPOIs.Select(p => new
            {
                poiId = p.Id,
                name = p.Name,
                distanceMeters = CalculateDistance(
                    request.Latitude, request.Longitude,
                    p.Latitude, p.Longitude) * 1000,
                shouldTriggerAudio = CalculateDistance(
                    request.Latitude, request.Longitude,
                    p.Latitude, p.Longitude) <= 0.05 // 50 meters
            })
        });
    }

    /// <summary>
    /// Log a visit when tourist scans QR or enters geofence
    /// </summary>
    [HttpPost("{touristId}/visits")]
    public async Task<ActionResult> LogVisit(int touristId, [FromBody] LogVisitRequest request)
    {
        var poiId = request.EffectivePoiId;
        if (poiId <= 0)
            return BadRequest(new { message = "Thiếu poiId hợp lệ" });

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
        {
            return NotFound(new { message = "Tourist không tồn tại" });
        }

        var poi = await _context.PointsOfInterest
            .FirstOrDefaultAsync(p => p.Id == poiId && !p.IsDeleted);

        if (poi == null)
        {
            return NotFound(new { message = "POI không tồn tại" });
        }

        // Dedupe only within a short cooldown window to keep heatmap/routes meaningful.
        var nowUtc = DateTime.UtcNow;
        var dedupeSinceUtc = nowUtc.AddMinutes(-5);
        var existingVisit = await _context.VisitLogs
            .FirstOrDefaultAsync(v =>
                v.TouristId == touristId &&
                v.PointOfInterestId == poiId &&
            v.VisitedAt >= dedupeSinceUtc);

        if (existingVisit == null)
        {
            var visitLog = new VisitLog
            {
                TouristId = touristId,
                PointOfInterestId = poiId,
                VisitedAt = nowUtc,
                VisitorLatitude = request.Latitude ?? tourist.LastLatitude ?? 0,
                VisitorLongitude = request.Longitude ?? tourist.LastLongitude ?? 0,
                LanguageUsed = string.IsNullOrWhiteSpace(request.LanguageCode)
                    ? (tourist.PreferredLanguage ?? "vi")
                    : request.LanguageCode
            };

            _context.VisitLogs.Add(visitLog);
            tourist.TotalVisits++;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Tourist {TouristId} visited POI {PoiId} via {TriggerMethod}",
                touristId,
                poiId,
                request.TriggerMethod ?? "unknown");
        }

        return Ok(new { success = true, message = "Visit logged successfully" });
    }

    /// <summary>
    /// Get tourist visit history
    /// </summary>
    [HttpGet("{touristId}/visits")]
    public async Task<ActionResult<List<VisitHistoryDto>>> GetVisitHistory(int touristId)
    {
        var visits = await _context.VisitLogs
            .Where(v => v.TouristId == touristId)
            .Include(v => v.PointOfInterest)
            .OrderByDescending(v => v.VisitedAt)
            .Take(50)
            .Select(v => new VisitHistoryDto
            {
                VisitId = v.Id,
                PoiId = v.PointOfInterestId,
                PoiName = v.PointOfInterest.Name,
                PoiImageUrl = v.PointOfInterest.ImageUrl,
                VisitedAt = v.VisitedAt
            })
            .ToListAsync();

        // Prepend base URL to relative image paths
        foreach (var v in visits)
            if (!string.IsNullOrEmpty(v.PoiImageUrl) && !v.PoiImageUrl.StartsWith("http"))
                v.PoiImageUrl = $"{Request.Scheme}://{Request.Host}{v.PoiImageUrl}";

        return Ok(visits);
    }

    /// <summary>
    /// Add POI to favorites
    /// </summary>
    [HttpPost("{touristId}/favorites")]
    public async Task<ActionResult> AddFavorite(int touristId, [FromBody] AddFavoriteRequest request)
    {
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
        {
            return NotFound(new { message = "Tourist không tồn tại" });
        }

        // Include soft-deleted records to prevent unique-index violation on re-add
        var existingFavorite = await _context.Set<Favorite>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == request.PoiId);

        if (existingFavorite != null)
        {
            if (!existingFavorite.IsDeleted)
            {
                // Already active – idempotent
                return Ok(new { success = true, message = "POI đã có trong danh sách yêu thích" });
            }

            // Re-activate a previously soft-deleted favorite
            existingFavorite.IsDeleted = false;
            existingFavorite.DeletedAt = null;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã thêm vào yêu thích" });
        }

        var favorite = new Favorite
        {
            TouristId = touristId,
            PointOfInterestId = request.PoiId,
            Note = request.Note
        };

        _context.Set<Favorite>().Add(favorite);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Đã thêm vào yêu thích" });
    }

    /// <summary>
    /// Remove POI from favorites
    /// </summary>
    [HttpDelete("{touristId}/favorites/{poiId}")]
    public async Task<ActionResult> RemoveFavorite(int touristId, int poiId)
    {
        // Include soft-deleted to make this idempotent
        var favorite = await _context.Set<Favorite>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == poiId);

        if (favorite == null || favorite.IsDeleted)
        {
            // Already removed – idempotent success so client state can sync cleanly
            return Ok(new { success = true, message = "Yêu thích đã được xóa" });
        }

        _context.Set<Favorite>().Remove(favorite);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Đã xóa khỏi yêu thích" });
    }

    /// <summary>
    /// Get tourist favorites
    /// </summary>
    [HttpGet("{touristId}/favorites")]
    public async Task<ActionResult<List<POIListItemDto>>> GetFavorites(
        int touristId,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var favoriteEntities = await _context.Set<Favorite>()
            .Where(f => f.TouristId == touristId)
            .Include(f => f.PointOfInterest)
                .ThenInclude(p => p.Category)
            .Include(f => f.PointOfInterest)
                .ThenInclude(p => p.Tags)
            .Include(f => f.PointOfInterest)
                .ThenInclude(p => p.Translations)
            .ToListAsync();

        var favorites = favoriteEntities
            .Select(f =>
            {
                var dto = new POIListItemDto
                {
                    PoiId = f.PointOfInterest.Id,
                    Name = f.PointOfInterest.Name,
                    Description = f.PointOfInterest.Description,
                    Latitude = f.PointOfInterest.Latitude,
                    Longitude = f.PointOfInterest.Longitude,
                    Address = f.PointOfInterest.Address,
                    ImageUrl = f.PointOfInterest.ImageUrl,
                    AverageRating = f.PointOfInterest.AverageRating,
                    TotalRatings = f.PointOfInterest.TotalRatings,
                    Category = f.PointOfInterest.Category?.Name ?? string.Empty,
                    Tags = f.PointOfInterest.Tags.Select(t => t.Name).ToList()
                };

                ApplyLocalizedFields(dto, f.PointOfInterest, normalizedLanguageCode);
                return dto;
            })
            .ToList();

        // Prepend base URL to relative image paths
        foreach (var fav in favorites)
            if (!string.IsNullOrEmpty(fav.ImageUrl) && !fav.ImageUrl.StartsWith("http"))
                fav.ImageUrl = $"{Request.Scheme}://{Request.Host}{fav.ImageUrl}";

        return Ok(favorites);
    }

    /// <summary>
    /// Submit rating for POI
    /// </summary>
    [HttpPost("{touristId}/ratings")]
    public async Task<ActionResult> SubmitRating(int touristId, [FromBody] SubmitRatingRequest request)
    {
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
        {
            return NotFound(new { message = "Tourist không tồn tại" });
        }

        var poi = await _context.PointsOfInterest
            .FirstOrDefaultAsync(p => p.Id == request.PoiId && !p.IsDeleted);

        if (poi == null)
        {
            return NotFound(new { message = "POI không tồn tại" });
        }

        // Check if already rated
        var existingRating = await _context.Set<Rating>()
            .FirstOrDefaultAsync(r => r.TouristId == touristId && r.PointOfInterestId == request.PoiId);

        if (existingRating != null)
        {
            // Update existing rating
            existingRating.Score = request.Score;
            existingRating.Comment = request.Comment;
            existingRating.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new rating
            var rating = new Rating
            {
                TouristId = touristId,
                PointOfInterestId = request.PoiId,
                Score = request.Score,
                Comment = request.Comment,
                LanguageCode = request.LanguageCode ?? "vi"
            };

            _context.Set<Rating>().Add(rating);
            poi.TotalRatings++;
        }

        // Recalculate average rating
        var allRatings = await _context.Set<Rating>()
            .Where(r => r.PointOfInterestId == request.PoiId)
            .ToListAsync();

        if (allRatings.Any())
        {
            poi.AverageRating = (decimal)allRatings.Average(r => r.Score);
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Cảm ơn đánh giá của bạn!" });
    }

    /// <summary>
    /// Thống kê hoạt động của tourist: số lượt thăm, audio play, ngôn ngữ yêu thích…
    /// </summary>
    [HttpGet("{touristId}/stats")]
    public async Task<ActionResult> GetStats(int touristId)
    {
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return NotFound(new { message = "Tourist không tồn tại" });

        // Lấy tất cả analytics events của tourist này
        var events = await _context.Analytics
            .Where(a => a.TouristId == touristId)
            .ToListAsync();

        // Audio minutes: tổng DurationSeconds của audio_complete events
        var totalAudioSeconds = events
            .Where(a => a.EventType == "audio_complete" && a.DurationSeconds > 0)
            .Sum(a => a.DurationSeconds ?? 0);

        // POI được thăm nhiều nhất
        var mostVisitedPoiId = await _context.VisitLogs
            .Where(v => v.TouristId == touristId)
            .GroupBy(v => v.PointOfInterestId)
            .OrderByDescending(g => g.Count())
            .Select(g => (int?)g.Key)
            .FirstOrDefaultAsync();

        string? mostVisitedPoiName = null;
        if (mostVisitedPoiId.HasValue)
        {
            mostVisitedPoiName = await _context.PointsOfInterest
                .Where(p => p.Id == mostVisitedPoiId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
        }

        // Ngôn ngữ yêu thích (xuất hiện nhiều nhất trong analytics)
        var favoriteLanguage = events
            .Where(a => !string.IsNullOrEmpty(a.LanguageCode))
            .GroupBy(a => a.LanguageCode)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? tourist.PreferredLanguage;

        return Ok(new
        {
            totalVisits = tourist.TotalVisits,
            totalAudioPlays = events.Count(a => a.EventType == "audio_play"),
            totalQRScans = events.Count(a => a.EventType == "qr_scan"),
            totalGeofenceEnters = events.Count(a => a.EventType == "geofence_enter"),
            totalAudioMinutes = Math.Round(totalAudioSeconds / 60.0, 1),
            mostVisitedPOI = mostVisitedPoiName,
            favoriteLanguage = favoriteLanguage
        });
    }

    private async Task<List<PointOfInterest>> CheckNearbyPOIs(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return new List<PointOfInterest>();
        }

        var allPOIs = await _context.PointsOfInterest
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync();

        // Find POIs within 200 meters
        return allPOIs
            .Where(p => CalculateDistance(latitude.Value, longitude.Value, p.Latitude, p.Longitude) <= 0.2)
            .ToList();
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static void ApplyLocalizedFields(POIListItemDto dto, PointOfInterest poi, string languageCode)
    {
        var translation = ResolveTranslation(poi, languageCode);
        if (translation == null)
            return;

        if (!string.IsNullOrWhiteSpace(translation.Name))
            dto.Name = translation.Name;

        if (!string.IsNullOrWhiteSpace(translation.Description))
            dto.Description = translation.Description;

        if (!string.IsNullOrWhiteSpace(translation.Address))
            dto.Address = translation.Address;
    }

    private static PointOfInterestTranslation? ResolveTranslation(PointOfInterest poi, string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return poi.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == normalized)
            ?? poi.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == LanguageConstants.Vietnamese);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return LanguageConstants.Vietnamese;

        var code = languageCode.Trim().ToLowerInvariant();
        var separatorIndex = code.IndexOfAny(new[] { '-', '_' });
        return separatorIndex > 0 ? code[..separatorIndex] : code;
    }
}
