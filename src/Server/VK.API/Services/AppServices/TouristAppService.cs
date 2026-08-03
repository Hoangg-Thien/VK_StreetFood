using Microsoft.EntityFrameworkCore;
using VK.API.Auth;
using VK.API.Common;
using VK.API.Extensions;
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
    private readonly IJwtTokenService _jwtTokenService;

    public TouristAppService(
        IRepository<Tourist> touristRepository,
        IRepository<PointOfInterest> poiRepository,
        IRepository<VisitLog> visitLogRepository,
        IRepository<Favorite> favoriteRepository,
        IRepository<Rating> ratingRepository,
        IRepository<Analytics> analyticsRepository,
        IUnitOfWork unitOfWork,
        ILogger<TouristAppService> logger,
        IHttpContextAccessor httpContextAccessor,
        IJwtTokenService jwtTokenService)
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
        _jwtTokenService = jwtTokenService;
    }

    public async Task<TouristDto> RegisterTouristAsync(RegisterTouristRequest request)
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

        var token = _jwtTokenService.GenerateTouristToken(tourist.Id);

        return new TouristDto
        {
            TouristId = tourist.Id,
            DeviceId = tourist.DeviceId,
            PreferredLanguage = tourist.PreferredLanguage,
            TotalVisits = tourist.TotalVisits,
            Token = token
        };
    }

    public async Task<ServiceResult<UpdateLocationResultDto>> UpdateLocationAsync(int touristId, UpdateLocationRequest request)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return ServiceResult<UpdateLocationResultDto>.NotFound("Tourist không tồn tại");

        tourist.LastLatitude = request.Latitude;
        tourist.LastLongitude = request.Longitude;
        await _unitOfWork.SaveChangesAsync();

        var nearbyPOIs = await CheckNearbyPOIs(request.Latitude, request.Longitude);

        return ServiceResult<UpdateLocationResultDto>.Success(new UpdateLocationResultDto
        {
            Success = true,
            NearbyPOIs = nearbyPOIs.Select(p => new NearbyPoiCheckDto
            {
                PoiId = p.Id,
                Name = p.Name,
                DistanceMeters = GeoHelper.CalculateDistanceKm(
                    request.Latitude, request.Longitude,
                    p.Latitude, p.Longitude) * 1000,
                ShouldTriggerAudio = GeoHelper.CalculateDistanceKm(
                    request.Latitude, request.Longitude,
                    p.Latitude, p.Longitude) <= 0.05
            }).ToList()
        });
    }

    public async Task<ServiceResult> LogVisitAsync(int touristId, LogVisitRequest request)
    {
        var poiId = request.EffectivePOIId;
        if (poiId <= 0)
            return ServiceResult.BadRequest("Thiếu poiId hợp lệ");

        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return ServiceResult.NotFound("Tourist không tồn tại");

        var poi = await _poiRepository.Query()
            .FirstOrDefaultAsync(p => p.Id == poiId && !p.IsDeleted);

        if (poi == null)
            return ServiceResult.NotFound("POI không tồn tại");

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

        return ServiceResult.Success();
    }

    public async Task<IReadOnlyList<VisitHistoryDto>> GetVisitHistoryAsync(int touristId)
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

        return visits;
    }

    public async Task<ServiceResult> AddFavoriteAsync(int touristId, AddFavoriteRequest request)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return ServiceResult.NotFound("Tourist không tồn tại");

        var existingFavorite = await _favoriteRepository.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == request.POIId);

        if (existingFavorite != null)
        {
            if (!existingFavorite.IsDeleted)
                return ServiceResult.Success();

            existingFavorite.IsDeleted = false;
            existingFavorite.DeletedAt = null;
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Success();
        }

        var favorite = new Favorite
        {
            TouristId = touristId,
            PointOfInterestId = request.POIId,
            Note = request.Note
        };

        await _favoriteRepository.AddAsync(favorite);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RemoveFavoriteAsync(int touristId, int poiId)
    {
        var favorite = await _favoriteRepository.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TouristId == touristId && f.PointOfInterestId == poiId);

        if (favorite == null || favorite.IsDeleted)
            return ServiceResult.Success();

        _favoriteRepository.Remove(favorite);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<IReadOnlyList<POIListItemDto>> GetFavoritesAsync(int touristId, string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = LocalizationHelper.NormalizeLanguageCode(languageCode);

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

                LocalizationHelper.ApplyLocalizedPoiFields(dto, f.PointOfInterest, normalizedLanguageCode);
                return dto;
            })
            .ToList();

        var baseUrl = CurrentBaseUrl();
        foreach (var fav in favorites)
            if (!string.IsNullOrEmpty(fav.ImageUrl) && !fav.ImageUrl.StartsWith("http"))
                fav.ImageUrl = $"{baseUrl}{fav.ImageUrl}";

        return favorites;
    }

    public async Task<ServiceResult> SubmitRatingAsync(int touristId, SubmitRatingRequest request)
    {
        if (request.Score < 1 || request.Score > 5)
            return ServiceResult.BadRequest("Điểm đánh giá phải từ 1 đến 5");

        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return ServiceResult.NotFound("Tourist không tồn tại");

        var poi = await _poiRepository.Query()
            .FirstOrDefaultAsync(p => p.Id == request.POIId && !p.IsDeleted);

        if (poi == null)
            return ServiceResult.NotFound("POI không tồn tại");

        var existingRating = await _ratingRepository.Query()
            .FirstOrDefaultAsync(r => r.TouristId == touristId && r.PointOfInterestId == request.POIId);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
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

            await _unitOfWork.SaveChangesAsync();

            var allRatings = await _ratingRepository.Query()
                .Where(r => r.PointOfInterestId == request.POIId)
                .ToListAsync();

            if (allRatings.Any())
            {
                poi.AverageRating = (decimal)allRatings.Average(r => r.Score);
                await _unitOfWork.SaveChangesAsync();
            }
        });

        return ServiceResult.Success();
    }

    public async Task<TouristStatsDto?> GetStatsAsync(int touristId)
    {
        var tourist = await _touristRepository.Query()
            .FirstOrDefaultAsync(t => t.Id == touristId && !t.IsDeleted);

        if (tourist == null)
            return null;

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

        return new TouristStatsDto
        {
            TotalVisits = tourist.TotalVisits,
            TotalAudioPlays = events.Count(a => a.EventType == "audio_play"),
            TotalQRScans = events.Count(a => a.EventType == "qr_scan"),
            TotalGeofenceEnters = events.Count(a => a.EventType == "geofence_enter"),
            TotalAudioMinutes = Math.Round(totalAudioSeconds / 60.0, 1),
            MostVisitedPOI = mostVisitedPoiName,
            FavoriteLanguage = favoriteLanguage
        };
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
            .Where(p => GeoHelper.CalculateDistanceKm(latitude.Value, longitude.Value, p.Latitude, p.Longitude) <= 0.2)
            .ToList();
    }
}
