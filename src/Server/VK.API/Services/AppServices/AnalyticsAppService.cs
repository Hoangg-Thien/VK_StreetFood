using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using VK.API.Models;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.API.Services.AppServices;

public class AnalyticsAppService : IAnalyticsAppService
{
    private readonly IRepository<Analytics> _analyticsRepository;
    private readonly IRepository<VisitLog> _visitLogRepository;
    private readonly IRepository<Rating> _ratingRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalyticsAppService> _logger;

    public AnalyticsAppService(
        IRepository<Analytics> analyticsRepository,
        IRepository<VisitLog> visitLogRepository,
        IRepository<Rating> ratingRepository,
        IRepository<PointOfInterest> poiRepository,
        IUnitOfWork unitOfWork,
        ILogger<AnalyticsAppService> logger)
    {
        _analyticsRepository = analyticsRepository;
        _visitLogRepository = visitLogRepository;
        _ratingRepository = ratingRepository;
        _poiRepository = poiRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IActionResult> RecordEventAsync(RecordEventRequest request)
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

            return new OkObjectResult(new { success = true, eventId = analyticsEvent.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording analytics event");
            return new ObjectResult(new { message = "Lỗi khi ghi nhận sự kiện" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> GetPOISummaryAsync(int poiId, DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var events = await _analyticsRepository.Query()
            .Where(a => a.PointOfInterestId == poiId && a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
            .ToListAsync();

        var summary = new
        {
            totalViews = events.Count(e => e.EventType == "view"),
            totalScans = events.Count(e => e.EventType == "qr_scan"),
            totalAudioPlays = events.Count(e => e.EventType == "audio_play"),
            totalAudioCompletes = events.Count(e => e.EventType == "audio_complete"),
            uniqueVisitors = events.Select(e => e.TouristId).Distinct().Count(),
            averageDuration = events.Where(e => e.DurationSeconds > 0).Average(e => (double?)e.DurationSeconds) ?? 0,
            languageBreakdown = events
                .Where(e => !string.IsNullOrEmpty(e.LanguageCode))
                .GroupBy(e => e.LanguageCode)
                .Select(g => new { language = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList(),
            eventsByDay = events
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToList()
        };

        return new OkObjectResult(summary);
    }

    public async Task<IActionResult> GetDashboardAsync(DateTime? from, DateTime? to)
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

        var dashboard = new
        {
            overview = new
            {
                totalEvents = events.Count,
                totalVisits = visits.Count,
                totalRatings = ratings.Count,
                uniqueVisitors = events.Select(e => e.TouristId).Distinct().Count(),
                averageRating = ratings.Any() ? ratings.Average(r => (double)r.Score) : 0
            },
            topPOIs = await _poiRepository.Query()
                .Include(p => p.Analytics)
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(p => new
                {
                    poiId = p.Id,
                    name = p.Name,
                    totalEvents = p.Analytics.Count(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate),
                    averageRating = p.AverageRating,
                    totalRatings = p.TotalRatings
                })
                .OrderByDescending(p => p.totalEvents)
                .Take(10)
                .ToListAsync(),
            eventsByType = events
                .GroupBy(e => e.EventType)
                .Select(g => new { eventType = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList(),
            visitorsByLanguage = events
                .Where(e => !string.IsNullOrEmpty(e.LanguageCode))
                .GroupBy(e => e.LanguageCode)
                .Select(g => new { language = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList(),
            dailyTrend = events
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key,
                    events = g.Count(),
                    uniqueVisitors = g.Select(e => e.TouristId).Distinct().Count()
                })
                .OrderBy(x => x.date)
                .ToList()
        };

        return new OkObjectResult(dashboard);
    }

    public async Task<IActionResult> GetTopPOIsAsync(int count = 10)
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
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                categoryName = p.Category?.Name,
                visitCount = visitCounts.GetValueOrDefault(p.Id, 0),
                audioPlayCount = audioCounts.GetValueOrDefault(p.Id, 0),
                averageRating = (double)p.AverageRating,
                averageListenMinutes = Math.Round(avgListenMinutes.GetValueOrDefault(p.Id, 0.0), 2)
            })
            .OrderByDescending(p => p.visitCount + p.audioPlayCount)
            .Take(count)
            .ToList();

        return new OkObjectResult(result);
    }

    public async Task<IActionResult> GetTopListenedPoisAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 10)
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

        var data = raw.Select(x => new
        {
            x.poiId,
            x.poiName,
            x.audioPlayCount,
            x.audioCompleteCount,
            x.uniqueListeners,
            completionRate = x.audioPlayCount > 0
                ? Math.Round(Math.Min(100, (double)x.audioCompleteCount * 100.0 / x.audioPlayCount), 2)
                : 0
        });

        return new OkObjectResult(data);
    }

    public async Task<IActionResult> GetAverageListenPerPoiAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 20)
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

        var data = raw.Select(x => new
        {
            x.poiId,
            x.poiName,
            averageDurationSeconds = Math.Round(x.averageDurationSeconds, 2),
            x.sampleCount
        });

        return new OkObjectResult(data);
    }

    public async Task<IActionResult> GetHeatmapAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var query = _visitLogRepository.Query()
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .Where(v => v.VisitorLatitude != 0 && v.VisitorLongitude != 0);

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(v => v.LanguageUsed == languageCode);

        if (poiId.HasValue)
            query = query.Where(v => v.PointOfInterestId == poiId.Value);

        var visitPoints = await query
            .Select(v => new { v.VisitorLatitude, v.VisitorLongitude })
            .ToListAsync();

        var points = visitPoints
            .GroupBy(v => new { Lat = Math.Round(v.VisitorLatitude, 4), Lng = Math.Round(v.VisitorLongitude, 4) })
            .Select(g => new { latitude = g.Key.Lat, longitude = g.Key.Lng, weight = g.Count() })
            .OrderByDescending(x => x.weight)
            .ToList();

        return new OkObjectResult(points);
    }

    public async Task<IActionResult> GetAnonymousRoutesAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 50)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var safeTake = Math.Clamp(take, 1, 200);

        var query = _visitLogRepository.Query()
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .Where(v => v.TouristId > 0)
            .Where(v => v.VisitorLatitude != 0 && v.VisitorLongitude != 0);

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(v => v.LanguageUsed == languageCode);

        if (poiId.HasValue)
            query = query.Where(v => v.PointOfInterestId == poiId.Value);

        var visits = await query.OrderBy(v => v.VisitedAt).ToListAsync();

        var routes = visits
            .GroupBy(v => v.TouristId)
            .Select(g => new
            {
                anonymousVisitorId = BuildAnonymousVisitorId(g.Key),
                pointCount = g.Count(),
                firstSeenAt = g.First().VisitedAt,
                lastSeenAt = g.Last().VisitedAt,
                points = g.Select(v => new
                {
                    latitude = v.VisitorLatitude,
                    longitude = v.VisitorLongitude,
                    visitedAt = v.VisitedAt
                }).ToList()
            })
            .OrderByDescending(x => x.pointCount)
            .Take(safeTake)
            .ToList();

        return new OkObjectResult(routes);
    }

    private static string BuildAnonymousVisitorId(int touristId)
    {
        var source = Encoding.UTF8.GetBytes($"tourist:{touristId}");
        var hash = SHA256.HashData(source);
        return $"anon-{Convert.ToHexString(hash)[..10].ToLowerInvariant()}";
    }
}
