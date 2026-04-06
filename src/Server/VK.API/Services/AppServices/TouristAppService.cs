using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public class TouristAppService : ITouristAppService
{
    private readonly IRepository<Tourist> _touristRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<VisitLog> _visitLogRepository;
    private readonly IRepository<Favorite> _favoriteRepository;
    private readonly IRepository<Rating> _ratingRepository;
    private readonly IRepository<Analytics> _analyticsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TouristAppService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TouristAppService(
        IRepository<Tourist> touristRepository,
        IRepository<PointOfInterest> poiRepository,
        IRepository<VisitLog> visitLogRepository,
        IRepository<Favorite> favoriteRepository,
        IRepository<Rating> ratingRepository,
        IRepository<Analytics> analyticsRepository,
        IUnitOfWork unitOfWork,
        ILogger<TouristAppService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _touristRepository = touristRepository;
        _poiRepository = poiRepository;
        _visitLogRepository = visitLogRepository;
        _favoriteRepository = favoriteRepository;
        _ratingRepository = ratingRepository;
        _analyticsRepository = analyticsRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IActionResult> RegisterTouristAsync(RegisterTouristRequest request)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.DeviceId == request.DeviceId && !t.IsDeleted);

        if (tourist == null)
        {
            tourist = new Tourist
            {
                DeviceId = request.DeviceId,
                PreferredLanguage = request.PreferredLanguage ?? "vi",
                LastLatitude = request.Latitude,
                LastLongitude = request.Longitude
            };

            await _touristRepository.AddAsync(tourist);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("New tourist registered with DeviceId: {DeviceId}", request.DeviceId);
        }
        else
        {
            tourist.PreferredLanguage = request.PreferredLanguage ?? tourist.PreferredLanguage;
            tourist.LastLatitude = request.Latitude ?? tourist.LastLatitude;
            tourist.LastLongitude = request.Longitude ?? tourist.LastLongitude;
            await _unitOfWork.SaveChangesAsync();
        }

        return new OkObjectResult(new TouristDto
        {
            TouristId = tourist.Id,
            DeviceId = tourist.DeviceId,
            PreferredLanguage = tourist.PreferredLanguage,
            TotalVisits = tourist.TotalVisits
        });
    }

    public async Task<IActionResult> UpdateLocationAsync(int touristId, UpdateLocationRequest request)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return new NotFoundObjectResult(new { message = "Tourist không tồn tại" });

        tourist.LastLatitude = request.Latitude;
        tourist.LastLongitude = request.Longitude;
        await _unitOfWork.SaveChangesAsync();

        var nearbyPOIs = await CheckNearbyPOIs(request.Latitude, request.Longitude);

        return new OkObjectResult(new
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
                    p.Latitude, p.Longitude) <= 0.05
            })
        });
    }

    public async Task<IActionResult> LogVisitAsync(int touristId, LogVisitRequest request)
    {
        var poiId = request.EffectivePOIId;
        if (poiId <= 0)
            return new BadRequestObjectResult(new { message = "Thiếu poiId hợp lệ" });

        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return new NotFoundObjectResult(new { message = "Tourist không tồn tại" });

        var poi = await _poiRepository.Query()
            .FirstOrDefaultAsync(p => p.Id == poiId && !p.IsDeleted);

        if (poi == null)
            return new NotFoundObjectResult(new { message = "POI không tồn tại" });

        var nowUtc = DateTime.UtcNow;
        var dedupeSinceUtc = nowUtc.AddMinutes(-5);
        var existingVisit = await _visitLogRepository.Query()
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

            await _visitLogRepository.AddAsync(visitLog);
            tourist.TotalVisits++;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Tourist {TouristId} visited POI {PoiId} via {TriggerMethod}",
                touristId,
                poiId,
                request.TriggerMethod ?? "unknown");
        }

        return new OkObjectResult(new { success = true, message = "Visit logged successfully" });
    }

    public async Task<IActionResult> GetVisitHistoryAsync(int touristId)
    {
        var visits = await _visitLogRepository.Query()
            .Where(v => v.TouristId == touristId)
            .Include(v => v.PointOfInterest)
            .OrderByDescending(v => v.VisitedAt)
            .Take(50)
            .Select(v => new VisitHistoryDto
            {
                VisitId = v.Id,
                POIId = v.PointOfInterestId,
                POIName = v.PointOfInterest.Name,
                POIImageUrl = v.PointOfInterest.ImageUrl,
                VisitedAt = v.VisitedAt
            })
            .ToListAsync();

        var baseUrl = CurrentBaseUrl();
        foreach (var v in visits)
            if (!string.IsNullOrEmpty(v.POIImageUrl) && !v.POIImageUrl.StartsWith("http"))
                v.POIImageUrl = $"{baseUrl}{v.POIImageUrl}";

        return new OkObjectResult(visits);
    }

    public async Task<IActionResult> AddFavoriteAsync(int touristId, AddFavoriteRequest request)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return new NotFoundObjectResult(new { message = "Tourist không tồn tại" });

        var existingFavorite = await _favoriteRepository.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == request.POIId);

        if (existingFavorite != null)
        {
            if (!existingFavorite.IsDeleted)
                return new OkObjectResult(new { success = true, message = "POI đã có trong danh sách yêu thích" });

            existingFavorite.IsDeleted = false;
            existingFavorite.DeletedAt = null;
            await _unitOfWork.SaveChangesAsync();
            return new OkObjectResult(new { success = true, message = "Đã thêm vào yêu thích" });
        }

        var favorite = new Favorite
        {
            TouristId = touristId,
            PointOfInterestId = request.POIId,
            Note = request.Note
        };

        await _favoriteRepository.AddAsync(favorite);
        await _unitOfWork.SaveChangesAsync();

        return new OkObjectResult(new { success = true, message = "Đã thêm vào yêu thích" });
    }

    public async Task<IActionResult> RemoveFavoriteAsync(int touristId, int poiId)
    {
        var favorite = await _favoriteRepository.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == poiId);

        if (favorite == null || favorite.IsDeleted)
            return new OkObjectResult(new { success = true, message = "Yêu thích đã được xóa" });

        _favoriteRepository.Remove(favorite);
        await _unitOfWork.SaveChangesAsync();

        return new OkObjectResult(new { success = true, message = "Đã xóa khỏi yêu thích" });
    }

    public async Task<IActionResult> GetFavoritesAsync(int touristId, string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var favoriteEntities = await _favoriteRepository.Query()
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
                    POIId = f.PointOfInterest.Id,
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

        var baseUrl = CurrentBaseUrl();
        foreach (var fav in favorites)
            if (!string.IsNullOrEmpty(fav.ImageUrl) && !fav.ImageUrl.StartsWith("http"))
                fav.ImageUrl = $"{baseUrl}{fav.ImageUrl}";

        return new OkObjectResult(favorites);
    }

    public async Task<IActionResult> SubmitRatingAsync(int touristId, SubmitRatingRequest request)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return new NotFoundObjectResult(new { message = "Tourist không tồn tại" });

        var poi = await _poiRepository.Query()
            .FirstOrDefaultAsync(p => p.Id == request.POIId && !p.IsDeleted);

        if (poi == null)
            return new NotFoundObjectResult(new { message = "POI không tồn tại" });

        var existingRating = await _ratingRepository.Query()
            .FirstOrDefaultAsync(r => r.TouristId == touristId && r.PointOfInterestId == request.POIId);

        if (existingRating != null)
        {
            existingRating.Score = request.Score;
            existingRating.Comment = request.Comment;
            existingRating.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var rating = new Rating
            {
                TouristId = touristId,
                PointOfInterestId = request.POIId,
                Score = request.Score,
                Comment = request.Comment,
                LanguageCode = request.LanguageCode ?? "vi"
            };

            await _ratingRepository.AddAsync(rating);
            poi.TotalRatings++;
        }

        var allRatings = await _ratingRepository.Query()
            .Where(r => r.PointOfInterestId == request.POIId)
            .ToListAsync();

        if (allRatings.Any())
            poi.AverageRating = (decimal)allRatings.Average(r => r.Score);

        await _unitOfWork.SaveChangesAsync();
        return new OkObjectResult(new { success = true, message = "Cảm ơn đánh giá của bạn!" });
    }

    public async Task<IActionResult> GetStatsAsync(int touristId)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return new NotFoundObjectResult(new { message = "Tourist không tồn tại" });

        var events = await _analyticsRepository.Query()
            .Where(a => a.TouristId == touristId)
            .ToListAsync();

        var totalAudioSeconds = events
            .Where(a => a.EventType == "audio_complete" && a.DurationSeconds > 0)
            .Sum(a => a.DurationSeconds ?? 0);

        var mostVisitedPoiId = await _visitLogRepository.Query()
            .Where(v => v.TouristId == touristId)
            .GroupBy(v => v.PointOfInterestId)
            .OrderByDescending(g => g.Count())
            .Select(g => (int?)g.Key)
            .FirstOrDefaultAsync();

        string? mostVisitedPoiName = null;
        if (mostVisitedPoiId.HasValue)
        {
            mostVisitedPoiName = await _poiRepository.Query()
                .Where(p => p.Id == mostVisitedPoiId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
        }

        var favoriteLanguage = events
            .Where(a => !string.IsNullOrEmpty(a.LanguageCode))
            .GroupBy(a => a.LanguageCode)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? tourist.PreferredLanguage;

        return new OkObjectResult(new
        {
            totalVisits = tourist.TotalVisits,
            totalAudioPlays = events.Count(a => a.EventType == "audio_play"),
            totalQRScans = events.Count(a => a.EventType == "qr_scan"),
            totalGeofenceEnters = events.Count(a => a.EventType == "geofence_enter"),
            totalAudioMinutes = Math.Round(totalAudioSeconds / 60.0, 1),
            mostVisitedPOI = mostVisitedPoiName,
            favoriteLanguage
        });
    }

    private string CurrentBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return string.Empty;

        return $"{request.Scheme}://{request.Host}";
    }

    private async Task<List<PointOfInterest>> CheckNearbyPOIs(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            return new List<PointOfInterest>();

        var allPOIs = await _poiRepository.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync();

        return allPOIs
            .Where(p => CalculateDistance(latitude.Value, longitude.Value, p.Latitude, p.Longitude) <= 0.2)
            .ToList();
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
