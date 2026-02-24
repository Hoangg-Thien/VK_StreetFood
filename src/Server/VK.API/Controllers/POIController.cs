using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Shared.DTOs;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class POIController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<POIController> _logger;

    public POIController(VKStreetFoodDbContext context, ILogger<POIController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all POIs (Points of Interest)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<POIListItemDto>>> GetAllPOIs(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null)
    {
        var query = _context.PointsOfInterest
            .Where(p => !p.IsDeleted && p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => 
                p.Name.Contains(search) || 
                p.Description.Contains(search) ||
                p.Address.Contains(search));
        }

        var pois = await query
            .OrderBy(p => p.Id)
            .Select(p => new POIListItemDto
            {
                PoiId = p.Id,
                Name = p.Name,
                Description = p.Description,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Address = p.Address,
                ImageUrl = p.ImageUrl,
                AverageRating = p.AverageRating,
                TotalRatings = p.TotalRatings,
                Category = p.Category!.Name,
                Tags = p.Tags.Select(t => t.Name).ToList()
            })
            .ToListAsync();

        return Ok(pois);
    }

    /// <summary>
    /// Get POIs near a specific location (GPS-based)
    /// </summary>
    [HttpGet("nearby")]
    public async Task<ActionResult<List<POIListItemDto>>> GetNearbyPOIs(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 1.0)
    {
        // Simple distance calculation using Haversine formula approximation
        var pois = await _context.PointsOfInterest
            .Where(p => !p.IsDeleted && p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .ToListAsync();

        var nearbyPois = pois
            .Select(p => new
            {
                Poi = p,
                Distance = CalculateDistance(latitude, longitude, p.Latitude, p.Longitude)
            })
            .Where(x => x.Distance <= radiusKm)
            .OrderBy(x => x.Distance)
            .Select(x => new POIListItemDto
            {
                PoiId = x.Poi.Id,
                Name = x.Poi.Name,
                Description = x.Poi.Description,
                Latitude = x.Poi.Latitude,
                Longitude = x.Poi.Longitude,
                Address = x.Poi.Address,
                ImageUrl = x.Poi.ImageUrl,
                AverageRating = x.Poi.AverageRating,
                TotalRatings = x.Poi.TotalRatings,
                Category = x.Poi.Category?.Name ?? "",
                Tags = x.Poi.Tags.Select(t => t.Name).ToList(),
                DistanceKm = x.Distance
            })
            .ToList();

        _logger.LogInformation("Found {Count} POIs within {Radius}km of ({Lat}, {Lng})", 
            nearbyPois.Count, radiusKm, latitude, longitude);

        return Ok(nearbyPois);
    }

    /// <summary>
    /// Get POI details by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<POIDetailDto>> GetPOIById(int id, [FromQuery] string languageCode = "vi")
    {
        var poi = await _context.PointsOfInterest
            .Include(p => p.Category)
            .Include(p => p.AudioContents)
            .Include(p => p.Vendors)
                .ThenInclude(v => v.Products)
            .Include(p => p.Vendors)
                .ThenInclude(v => v.OpeningHours)
            .Include(p => p.Tags)
            .Include(p => p.Ratings)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poi == null)
        {
            return NotFound(new { message = "POI không tồn tại" });
        }

        var audio = poi.AudioContents.FirstOrDefault(a => a.LanguageCode == languageCode)
                   ?? poi.AudioContents.FirstOrDefault(a => a.LanguageCode == "vi");

        var response = new POIDetailDto
        {
            PoiId = poi.Id,
            Name = poi.Name,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            Address = poi.Address,
            ImageUrl = poi.ImageUrl,
            QRCode = poi.QRCode,
            AverageRating = poi.AverageRating,
            TotalRatings = poi.TotalRatings,
            Category = poi.Category?.Name,
            Tags = poi.Tags.Select(t => t.Name).ToList(),
            Audio = audio != null ? new AudioContentDto
            {
                AudioId = audio.Id,
                LanguageCode = audio.LanguageCode,
                AudioFileUrl = audio.AudioFileUrl,
                TextContent = audio.TextContent,
                DurationInSeconds = audio.DurationInSeconds
            } : null,
            Vendors = poi.Vendors.Select(v => new VendorDetailDto
            {
                VendorId = v.Id,
                Name = v.Name,
                Description = v.Description,
                PhoneNumber = v.PhoneNumber,
                Email = v.Email,
                AverageRating = v.AverageRating,
                TotalReviews = v.TotalReviews,
                ImageUrl = v.ImageUrl,
                Products = v.Products.Where(p => p.IsAvailable).Select(p => new ProductDto
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl
                }).ToList(),
                OpeningHours = v.OpeningHours.Select(oh => new OpeningHoursDto
                {
                    DayOfWeek = oh.DayOfWeek,
                    OpenTime = oh.OpenTime.ToString(@"hh\:mm"),
                    CloseTime = oh.CloseTime.ToString(@"hh\:mm"),
                    IsClosed = oh.IsClosed
                }).ToList()
            }).ToList(),
            RecentRatings = poi.Ratings
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new RatingDto
                {
                    Score = r.Score,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
    {
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategoryDto
            {
                CategoryId = c.Id,
                Name = c.Name,
                Description = c.Description,
                IconUrl = c.IconUrl
            })
            .ToListAsync();

        return Ok(categories);
    }

    // Haversine formula for distance calculation
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
