using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public class POIAppService : IPOIAppService
{
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly ILogger<POIAppService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private static readonly IReadOnlyDictionary<int, (int Priority, double? TriggerRadiusMeters)> PoiTriggerProfiles
        = new Dictionary<int, (int Priority, double? TriggerRadiusMeters)>
        {
            [1] = (100, 80),
            [2] = (70, 55),
            [3] = (68, 55),
            [4] = (66, 55),
            [5] = (85, 60),
            [6] = (62, 60),
            [7] = (60, 60),
            [8] = (58, 55),
            [9] = (64, 55),
            [10] = (56, 60),
            [11] = (57, 55),
            [12] = (54, 50)
        };

    public POIAppService(
        IRepository<PointOfInterest> poiRepository,
        IRepository<Category> categoryRepository,
        ILogger<POIAppService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _poiRepository = poiRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IActionResult> GetAllPOIsAsync(
        int? categoryId = null, 
        string? search = null, 
        string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var query = _poiRepository.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .Include(p => p.Translations)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

          if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
            p.Name.ToLower().Contains(searchLower) ||
            p.Description.ToLower().Contains(searchLower) ||
            p.Address.ToLower().Contains(searchLower));
        }

        var entities = await query.OrderBy(p => p.Id).ToListAsync();

        var pois = entities.Select(p =>
        {
        var dto = new POIListItemDto
        {
            POIId = p.Id,
            Name = p.Name,
            Description = p.Description,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Address = p.Address,
            ImageUrl = p.ImageUrl,
            AverageRating = p.AverageRating,
            TotalRatings = p.TotalRatings,
            Category = p.Category?.Name ?? string.Empty,
            Tags = p.Tags.Select(t => t.Name).ToList()
        };

        ApplyLocalizedFields(dto, p, normalizedLanguageCode);
        return dto;
    }).ToList();


    var baseUrl = CurrentBaseUrl();
    foreach (var poi in pois)
    {
        poi.ImageUrl = PrependBase(baseUrl, poi.ImageUrl);
        var profile = GetTriggerProfile(poi.POIId);
        poi.Priority = profile.Priority;
        poi.TriggerRadiusMeters = profile.TriggerRadiusMeters;
    }

    return new OkObjectResult(pois);
    }

    public async Task<IActionResult> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var pois = await _poiRepository.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .Include(p => p.Translations)
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
                POIId = x.Poi.Id,
                Name = x.Poi.Name,
                Description = x.Poi.Description,
                Latitude = x.Poi.Latitude,
                Longitude = x.Poi.Longitude,
                Address = x.Poi.Address,
                ImageUrl = FullUrl(x.Poi.ImageUrl),
                AverageRating = x.Poi.AverageRating,
                TotalRatings = x.Poi.TotalRatings,
                Category = x.Poi.Category?.Name ?? string.Empty,
                Tags = x.Poi.Tags.Select(t => t.Name).ToList(),
                DistanceKm = x.Distance,
                Priority = GetTriggerProfile(x.Poi.Id).Priority,
                TriggerRadiusMeters = GetTriggerProfile(x.Poi.Id).TriggerRadiusMeters
            })
            .ToList();

        foreach (var item in nearbyPois)
        {
            var source = pois.First(p => p.Id == item.POIId);
            ApplyLocalizedFields(item, source, normalizedLanguageCode);
        }

        _logger.LogInformation("Found {Count} POIs within {Radius}km of ({Lat}, {Lng})",
            nearbyPois.Count, radiusKm, latitude, longitude);

        return new OkObjectResult(nearbyPois);
    }

    public async Task<IActionResult> GetPOIByIdAsync(int id, string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var poi = await _poiRepository.Query()
            .Include(p => p.Category)
            .Include(p => p.AudioContents)
            .Include(p => p.Translations)
            .Include(p => p.Vendors)
                .ThenInclude(v => v.OpeningHours)
            .Include(p => p.Tags)
            .Include(p => p.Ratings)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poi == null)
            return new NotFoundObjectResult(new { message = "POI không tồn tại" });

        var audio = poi.AudioContents.FirstOrDefault(a => a.LanguageCode == normalizedLanguageCode)
               ?? poi.AudioContents.FirstOrDefault(a => a.LanguageCode == LanguageConstants.Vietnamese);

        var response = new POIDetailDto
        {
            POIId = poi.Id,
            Name = poi.Name,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            Address = poi.Address,
            ImageUrl = FullUrl(poi.ImageUrl),
            AverageRating = poi.AverageRating,
            TotalRatings = poi.TotalRatings,
            Category = poi.Category?.Name ?? string.Empty,
            Tags = poi.Tags.Select(t => t.Name).ToList(),
            Priority = GetTriggerProfile(poi.Id).Priority,
            TriggerRadiusMeters = GetTriggerProfile(poi.Id).TriggerRadiusMeters,
            Audio = audio != null ? new AudioContentDto
            {
                AudioId = audio.Id,
                LanguageCode = audio.LanguageCode,
                TextContent = audio.TextContent,
                AudioFileUrl = audio.AudioFileUrl != null ? FullUrl(audio.AudioFileUrl) : null,
                IsGenerated = audio.IsGenerated,
                DurationSeconds = audio.DurationSeconds
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
                ImageUrl = FullUrl(v.ImageUrl),
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

        ApplyLocalizedFields(response, poi, normalizedLanguageCode);
        return new OkObjectResult(response);
    }

    public async Task<IActionResult> GetCategoriesAsync()
    {
        var categories = await _categoryRepository.Query()
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

        return new OkObjectResult(categories);
    }

    private string CurrentBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return string.Empty;

        return $"{request.Scheme}://{request.Host}";
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private string? FullUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return PrependBase(CurrentBaseUrl(), path);
    }

    private static string? PrependBase(string baseUrl, string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
        if (string.IsNullOrWhiteSpace(baseUrl)) return path;
        return baseUrl + path;
    }

    private static (int Priority, double? TriggerRadiusMeters) GetTriggerProfile(int poiId)
        => PoiTriggerProfiles.TryGetValue(poiId, out var profile)
            ? profile
            : (0, null);

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
