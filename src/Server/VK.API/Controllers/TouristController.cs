using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;
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

        // Check if already visited today
        var today = DateTime.UtcNow.Date;
        var existingVisit = await _context.VisitLogs
            .FirstOrDefaultAsync(v => 
                v.TouristId == touristId && 
                v.PointOfInterestId == request.PoiId &&
                v.VisitedAt.Date == today);

        if (existingVisit == null)
        {
            var visitLog = new VisitLog
            {
                TouristId = touristId,
                PointOfInterestId = request.PoiId,
                VisitedAt = DateTime.UtcNow,
                DurationMinutes = 0
            };

            _context.VisitLogs.Add(visitLog);
            tourist.TotalVisits++;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tourist {TouristId} visited POI {PoiId}", touristId, request.PoiId);
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
                VisitedAt = v.VisitedAt,
                DurationMinutes = v.DurationMinutes
            })
            .ToListAsync();

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

        var existingFavorite = await _context.Set<Favorite>()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == request.PoiId);

        if (existingFavorite != null)
        {
            return BadRequest(new { message = "POI đã có trong danh sách yêu thích" });
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
        var favorite = await _context.Set<Favorite>()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == poiId);

        if (favorite == null)
        {
            return NotFound(new { message = "Yêu thích không tồn tại" });
        }

        _context.Set<Favorite>().Remove(favorite);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Đã xóa khỏi yêu thích" });
    }

    /// <summary>
    /// Get tourist favorites
    /// </summary>
    [HttpGet("{touristId}/favorites")]
    public async Task<ActionResult<List<POIListItemDto>>> GetFavorites(int touristId)
    {
        var favorites = await _context.Set<Favorite>()
            .Where(f => f.TouristId == touristId)
            .Include(f => f.PointOfInterest)
                .ThenInclude(p => p.Category)
            .Include(f => f.PointOfInterest)
                .ThenInclude(p => p.Tags)
            .Select(f => new POIListItemDto
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
                Category = f.PointOfInterest.Category!.Name,
                Tags = f.PointOfInterest.Tags.Select(t => t.Name).ToList()
            })
            .ToListAsync();

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
}
