using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using VK.API.Common;
using VK.API.Models;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public class AnalyticsAppService : IAnalyticsAppService
{
    private readonly IRepository<Analytics> _analyticsRepository;
    private readonly IRepository<VisitLog> _visitLogRepository;
    private readonly IRepository<Rating> _ratingRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<Tourist> _touristRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalyticsAppService> _logger;

    public AnalyticsAppService(
        IRepository<Analytics> analyticsRepository,
        IRepository<VisitLog> visitLogRepository,
        IRepository<Rating> ratingRepository,
        IRepository<PointOfInterest> poiRepository,
        IRepository<Tourist> touristRepository,
        IUnitOfWork unitOfWork,
        ILogger<AnalyticsAppService> logger)
    {
        _analyticsRepository = analyticsRepository;
        _visitLogRepository = visitLogRepository;
        _ratingRepository = ratingRepository;
        _poiRepository = poiRepository;
        _touristRepository = touristRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<RecordEventResultDto>> RecordEventAsync(RecordEventRequest request)
    {
        try
        {
            var normalizedEventType = (request.EventType ?? string.Empty).Trim().ToLowerInvariant();

            var analyticsEvent = new Analytics
            {
                TouristId = request.TouristId,
                PointOfInterestId = request.POIId,
                EventType = normalizedEventType,
                LanguageCode = request.LanguageCode ?? "vi",
                DurationSeconds = request.DurationSeconds
            };

            await _analyticsRepository.AddAsync(analyticsEvent);

            if (normalizedEventType == "audio_play" && request.TouristId.HasValue)
            {
                var lookbackUtc = DateTime.UtcNow.AddDays(-1);
                var latestVisit = await _visitLogRepository.Query()
                    .Where(v => v.TouristId == request.TouristId.Value)
                    .Where(v => v.PointOfInterestId == request.POIId)
                    .Where(v => v.VisitedAt >= lookbackUtc)
                    .OrderByDescending(v => v.VisitedAt)
                    .FirstOrDefaultAsync();

                if (latestVisit != null)
                {
                    latestVisit.AudioPlayed = true;
                    if (string.IsNullOrWhiteSpace(latestVisit.LanguageUsed) && !string.IsNullOrWhiteSpace(request.LanguageCode))
                        latestVisit.LanguageUsed = request.LanguageCode;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Analytics event recorded: {EventType} for POI {PoiId} by Tourist {TouristId}",
                request.EventType, request.POIId, request.TouristId);

            return ServiceResult<RecordEventResultDto>.Success(new RecordEventResultDto
            {
                Success = true,
                EventId = analyticsEvent.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording analytics event");
            return ServiceResult<RecordEventResultDto>.Error("Lỗi khi ghi nhận sự kiện");
        }
    }

    public async Task<ServiceResult<POISummaryDto>> GetPOISummaryAsync(int poiId, DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var events = await _analyticsRepository.Query()
            .Where(a => a.PointOfInterestId == poiId && a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
            .ToListAsync();

        var summary = new POISummaryDto
        {
            TotalViews = events.Count(e => e.EventType == "view"),
            TotalScans = events.Count(e => e.EventType == "qr_scan"),
            TotalAudioPlays = events.Count(e => e.EventType == "audio_play"),
            TotalAudioCompletes = events.Count(e => e.EventType == "audio_complete"),
            UniqueVisitors = events.Select(e => e.TouristId).Distinct().Count(),
            AverageDuration = events.Where(e => e.DurationSeconds > 0).Average(e => (double?)e.DurationSeconds) ?? 0,
            LanguageBreakdown = events
                .Where(e => !string.IsNullOrEmpty(e.LanguageCode))
                .GroupBy(e => e.LanguageCode)
                .Select(g => new LanguageBreakdownDto { Language = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList(),
            EventsByDay = events
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new EventsByDayDto { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList()
        };

        return ServiceResult<POISummaryDto>.Success(summary);
    }

    public async Task<ServiceResult<DashboardDto>> GetDashboardAsync(DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var events = await _analyticsRepository.Query()
            .Where(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
            .ToListAsync();

        var visits = await _visitLogRepository.Query()
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .ToListAsync();

        var ratings = await _ratingRepository.Query()
            .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
            .ToListAsync();

        var topPois = await _poiRepository.Query()
            .Include(p => p.Analytics)
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => new DashboardTopPoiDto
            {
                PoiId = p.Id,
                Name = p.Name,
                TotalEvents = p.Analytics.Count(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate),
                AverageRating = p.AverageRating,
                TotalRatings = p.TotalRatings
            })
            .OrderByDescending(p => p.TotalEvents)
            .Take(10)
            .ToListAsync();

        var dashboard = new DashboardDto
        {
            Overview = new DashboardOverviewDto
            {
                TotalEvents = events.Count,
                TotalVisits = visits.Count,
                TotalRatings = ratings.Count,
                UniqueVisitors = events.Select(e => e.TouristId).Distinct().Count(),
                AverageRating = ratings.Any() ? ratings.Average(r => (double)r.Score) : 0
            },
            TopPOIs = topPois,
            EventsByType = events
                .GroupBy(e => e.EventType)
                .Select(g => new EventsByTypeDto { EventType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList(),
            VisitorsByLanguage = events
                .Where(e => !string.IsNullOrEmpty(e.LanguageCode))
                .GroupBy(e => e.LanguageCode)
                .Select(g => new LanguageBreakdownDto { Language = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList(),
            DailyTrend = events
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new DailyTrendDto
                {
                    Date = g.Key,
                    Events = g.Count(),
                    UniqueVisitors = g.Select(e => e.TouristId).Distinct().Count()
                })
                .OrderBy(x => x.Date)
                .ToList()
        };

        return ServiceResult<DashboardDto>.Success(dashboard);
    }

    public async Task<ServiceResult<IReadOnlyList<TopPoiDto>>> GetTopPOIsAsync(int count = 10)
    {
        var pois = await _poiRepository.Query()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync();

        var visitCounts = await _visitLogRepository.Query()
            .GroupBy(v => v.PointOfInterestId)
            .Select(g => new { PoiId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PoiId, x => x.Count);

        var audioCounts = await _analyticsRepository.Query()
            .Where(a => a.EventType == "audio_play")
            .GroupBy(a => a.PointOfInterestId)
            .Select(g => new { PoiId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PoiId, x => x.Count);

        var avgListenMinutes = await _analyticsRepository.Query()
            .Where(a => a.EventType == "audio_complete" && a.DurationSeconds > 0)
            .GroupBy(a => a.PointOfInterestId)
            .Select(g => new { PoiId = g.Key, AvgMin = g.Average(a => (double)a.DurationSeconds!.Value) / 60.0 })
            .ToDictionaryAsync(x => x.PoiId, x => x.AvgMin);

        var result = pois
            .Select(p => new TopPoiDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category?.Name,
                VisitCount = visitCounts.GetValueOrDefault(p.Id, 0),
                AudioPlayCount = audioCounts.GetValueOrDefault(p.Id, 0),
                AverageRating = (double)p.AverageRating,
                AverageListenMinutes = Math.Round(avgListenMinutes.GetValueOrDefault(p.Id, 0.0), 2)
            })
            .OrderByDescending(p => p.VisitCount + p.AudioPlayCount)
            .Take(count)
            .ToList();

        return ServiceResult<IReadOnlyList<TopPoiDto>>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<TopListenedPoiDto>>> GetTopListenedPoisAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 10)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var safeTake = Math.Clamp(take, 1, 100);

        var query = _analyticsRepository.Query()
            .Where(a => a.EventTimestamp >= fromDate && a.EventTimestamp <= toDate)
            .Where(a => a.EventType == "audio_play" || a.EventType == "audio_complete");

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(a => a.LanguageCode == languageCode);

        if (poiId.HasValue)
            query = query.Where(a => a.PointOfInterestId == poiId.Value);

        var raw = await query
            .GroupBy(a => a.PointOfInterestId)
            .Select(g => new
            {
                poiId = g.Key,
                audioPlayCount = g.Count(x => x.EventType == "audio_play"),
                audioCompleteCount = g.Count(x => x.EventType == "audio_complete"),
                uniqueListeners = g
                    .Where(x => x.EventType == "audio_play" && x.TouristId != null)
                    .Select(x => x.TouristId)
                    .Distinct()
                    .Count()
            })
            .Where(x => x.audioPlayCount > 0)
            .Join(
                _poiRepository.Query().Where(p => !p.IsDeleted),
                a => a.poiId,
                p => p.Id,
                (a, p) => new
                {
                    poiId = p.Id,
                    poiName = p.Name,
                    a.audioPlayCount,
                    a.audioCompleteCount,
                    a.uniqueListeners
                })
            .OrderByDescending(x => x.audioPlayCount)
            .ThenByDescending(x => x.uniqueListeners)
            .ThenByDescending(x => x.audioCompleteCount)
            .Take(safeTake)
            .ToListAsync();

        var data = raw.Select(x => new TopListenedPoiDto
        {
            PoiId = x.poiId,
            PoiName = x.poiName,
            AudioPlayCount = x.audioPlayCount,
            AudioCompleteCount = x.audioCompleteCount,
            UniqueListeners = x.uniqueListeners,
            CompletionRate = x.audioPlayCount > 0
                ? Math.Round(Math.Min(100, (double)x.audioCompleteCount * 100.0 / x.audioPlayCount), 2)
                : 0
        }).ToList();

        return ServiceResult<IReadOnlyList<TopListenedPoiDto>>.Success(data);
    }

    public async Task<ServiceResult<IReadOnlyList<AvgListenPoiDto>>> GetAverageListenPerPoiAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 20)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var safeTake = Math.Clamp(take, 1, 200);

        var query = _analyticsRepository.Query()
            .Where(a => a.EventTimestamp >= fromDate && a.EventTimestamp <= toDate)
            .Where(a => a.DurationSeconds != null && a.DurationSeconds > 0)
            .Where(a => a.EventType == "audio_complete");

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(a => a.LanguageCode == languageCode);

        if (poiId.HasValue)
            query = query.Where(a => a.PointOfInterestId == poiId.Value);

        var raw = await query
            .GroupBy(a => a.PointOfInterestId)
            .Select(g => new
            {
                poiId = g.Key,
                averageDurationSeconds = g.Average(x => (double)x.DurationSeconds!.Value),
                sampleCount = g.Count()
            })
            .Join(
                _poiRepository.Query().Where(p => !p.IsDeleted),
                a => a.poiId,
                p => p.Id,
                (a, p) => new
                {
                    poiId = p.Id,
                    poiName = p.Name,
                    averageDurationSeconds = a.averageDurationSeconds,
                    a.sampleCount
                })
            .OrderByDescending(x => x.averageDurationSeconds)
            .Take(safeTake)
            .ToListAsync();

        var data = raw.Select(x => new AvgListenPoiDto
        {
            PoiId = x.poiId,
            PoiName = x.poiName,
            AverageDurationSeconds = Math.Round(x.averageDurationSeconds, 2),
            SampleCount = x.sampleCount
        }).ToList();

        return ServiceResult<IReadOnlyList<AvgListenPoiDto>>.Success(data);
    }

    public async Task<ServiceResult<IReadOnlyList<HeatmapPointDto>>> GetHeatmapAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId)
    {
        // Lấy heatmap từ vị trí cuối cùng của mỗi Tourist
        var query = _touristRepository.Query()
            .Where(t => t.LastLatitude != null && t.LastLongitude != null)
            .Where(t => t.LastLatitude != 0 && t.LastLongitude != 0);

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(t => t.PreferredLanguage == languageCode);

        // Không lọc theo POI vì Tourist không có POI

        var points = await query
            .Select(t => new { Lat = Math.Round(t.LastLatitude!.Value, 4), Lng = Math.Round(t.LastLongitude!.Value, 4) })
            .ToListAsync();

        var grouped = points
            .GroupBy(v => new { v.Lat, v.Lng })
            .Select(g => new HeatmapPointDto { Latitude = g.Key.Lat, Longitude = g.Key.Lng, Weight = g.Count() })
            .OrderByDescending(x => x.Weight)
            .ToList();

        return ServiceResult<IReadOnlyList<HeatmapPointDto>>.Success(grouped);
    }

    public async Task<ServiceResult<IReadOnlyList<AnonymousRouteDto>>> GetAnonymousRoutesAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 50)
    {
        // Tuyến ẩn danh: mỗi tourist là 1 tuyến, chỉ lấy điểm cuối cùng
        var safeTake = Math.Clamp(take, 1, 200);
        var query = _touristRepository.Query()
            .Where(t => t.LastLatitude != null && t.LastLongitude != null)
            .Where(t => t.LastLatitude != 0 && t.LastLongitude != 0);

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(t => t.PreferredLanguage == languageCode);

        var tourists = await query.OrderByDescending(t => t.LastLocationUpdate).Take(safeTake).ToListAsync();

        var routes = tourists.Select(t => new AnonymousRouteDto
        {
            AnonymousVisitorId = BuildAnonymousVisitorId(t.Id),
            PointCount = 1,
            FirstSeenAt = t.LastLocationUpdate,
            LastSeenAt = t.LastLocationUpdate,
            Points = new List<AnonymousRoutePointDto>
            {
                new AnonymousRoutePointDto
                {
                    Latitude = t.LastLatitude,
                    Longitude = t.LastLongitude,
                    VisitedAt = t.LastLocationUpdate
                }
            }
        }).ToList();

        return ServiceResult<IReadOnlyList<AnonymousRouteDto>>.Success(routes);
    }

    private static string BuildAnonymousVisitorId(int touristId)
    {
        var source = Encoding.UTF8.GetBytes($"tourist:{touristId}");
        var hash = SHA256.HashData(source);
        return $"anon-{Convert.ToHexString(hash)[..10].ToLowerInvariant()}";
    }
}
