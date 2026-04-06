using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(VKStreetFoodDbContext context, ILogger<AnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Record analytics event (view, qr_scan, audio_play, audio_complete)
    /// </summary>
    [HttpPost("event")]
    public async Task<ActionResult> RecordEvent([FromBody] RecordEventRequest request)
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

            _context.Analytics.Add(analyticsEvent);

            // Keep VisitLog.AudioPlayed in sync so UsageHistory cards remain consistent.
            if (normalizedEventType == "audio_play" && request.TouristId.HasValue)
            {
                var lookbackUtc = DateTime.UtcNow.AddDays(-1);
                var latestVisit = await _context.VisitLogs
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

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Analytics event recorded: {EventType} for POI {PoiId} by Tourist {TouristId}",
                request.EventType, request.POIId, request.TouristId);

            return Ok(new { success = true, eventId = analyticsEvent.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording analytics event");
            return StatusCode(500, new { message = "Lỗi khi ghi nhận sự kiện" });
        }
    }

    /// <summary>
    /// Get analytics summary for a POI
    /// </summary>
    [HttpGet("poi/{poiId}/summary")]
    public async Task<ActionResult> GetPOISummary(int poiId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var events = await _context.Analytics
            .Where(a => a.PointOfInterestId == poiId &&
                       a.CreatedAt >= fromDate &&
                       a.CreatedAt <= toDate)
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

        return Ok(summary);
    }

    /// <summary>
    /// Get overall analytics dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var events = await _context.Analytics
            .Where(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
            .ToListAsync();

        var visits = await _context.VisitLogs
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .ToListAsync();

        var ratings = await _context.Set<Rating>()
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
            topPOIs = await _context.PointsOfInterest
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

        return Ok(dashboard);
    }

    /// <summary>
    /// Top N POIs theo lượt visit + audio play.
    /// Được gọi bởi MAUI app (AnalyticsPage) để hiển thị bảng xếp hạng.
    /// </summary>
    [HttpGet("top-pois")]
    public async Task<ActionResult> GetTopPOIs([FromQuery] int count = 10)
    {
        // Lấy danh sách POI cùng category
        var pois = await _context.PointsOfInterest
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync();

        // Visit counts per POI (từ VisitLogs)
        var visitCounts = await _context.VisitLogs
            .GroupBy(v => v.PointOfInterestId)
            .Select(g => new { PoiId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PoiId, x => x.Count);

        // Audio play counts per POI
        var audioCounts = await _context.Analytics
            .Where(a => a.EventType == "audio_play")
            .GroupBy(a => a.PointOfInterestId)
            .Select(g => new { PoiId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PoiId, x => x.Count);

        // Average listen duration (phút) per POI
        var avgListenMinutes = await _context.Analytics
            .Where(a => a.EventType == "audio_complete" && a.DurationSeconds > 0)
            .GroupBy(a => a.PointOfInterestId)
            .Select(g => new
            {
                PoiId = g.Key,
                AvgMin = g.Average(a => (double)a.DurationSeconds!.Value) / 60.0
            })
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

        return Ok(result);
    }

    /// <summary>
    /// Top địa điểm được nghe nhiều nhất (audio_play/audio_complete).
    /// </summary>
    [HttpGet("top-listened-pois")]
    public async Task<ActionResult> GetTopListenedPois(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 10)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var safeTake = Math.Clamp(take, 1, 100);

        var query = _context.Analytics
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
                _context.PointsOfInterest.Where(p => !p.IsDeleted),
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

        return Ok(data);
    }

    /// <summary>
    /// Thời gian nghe trung bình theo POI (giây).
    /// </summary>
    [HttpGet("avg-listen-per-poi")]
    public async Task<ActionResult> GetAverageListenPerPoi(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 20)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var safeTake = Math.Clamp(take, 1, 200);

        var query = _context.Analytics
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
                _context.PointsOfInterest.Where(p => !p.IsDeleted),
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

        return Ok(data);
    }

    /// <summary>
    /// Heatmap vị trí người dùng từ VisitLog.
    /// </summary>
    [HttpGet("heatmap")]
    public async Task<ActionResult> GetHeatmap(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var query = _context.VisitLogs
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .Where(v => v.VisitorLatitude != 0 && v.VisitorLongitude != 0);

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(v => v.LanguageUsed == languageCode);

        if (poiId.HasValue)
            query = query.Where(v => v.PointOfInterestId == poiId.Value);

        var visitPoints = await query
            .Select(v => new
            {
                v.VisitorLatitude,
                v.VisitorLongitude
            })
            .ToListAsync();

        var points = visitPoints
            .GroupBy(v => new
            {
                Lat = Math.Round(v.VisitorLatitude, 4),
                Lng = Math.Round(v.VisitorLongitude, 4)
            })
            .Select(g => new
            {
                latitude = g.Key.Lat,
                longitude = g.Key.Lng,
                weight = g.Count()
            })
            .OrderByDescending(x => x.weight)
            .ToList();

        return Ok(points);
    }

    /// <summary>
    /// Tuyến di chuyển ẩn danh theo từng khách (không trả TouristId gốc).
    /// </summary>
    [HttpGet("anonymous-routes")]
    public async Task<ActionResult> GetAnonymousRoutes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 50)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var safeTake = Math.Clamp(take, 1, 200);

        var query = _context.VisitLogs
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .Where(v => v.TouristId > 0)
            .Where(v => v.VisitorLatitude != 0 && v.VisitorLongitude != 0);

        if (!string.IsNullOrWhiteSpace(languageCode))
            query = query.Where(v => v.LanguageUsed == languageCode);

        if (poiId.HasValue)
            query = query.Where(v => v.PointOfInterestId == poiId.Value);

        var visits = await query
            .OrderBy(v => v.VisitedAt)
            .ToListAsync();

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

        return Ok(routes);
    }

    private static string BuildAnonymousVisitorId(int touristId)
    {
        var source = Encoding.UTF8.GetBytes($"tourist:{touristId}");
        var hash = SHA256.HashData(source);
        return $"anon-{Convert.ToHexString(hash)[..10].ToLowerInvariant()}";
    }
}

public class RecordEventRequest
{
    public int? TouristId { get; set; }
    [JsonPropertyName("poiId")]
    public int POIId { get; set; }
    public string EventType { get; set; } = string.Empty; // view, qr_scan, audio_play, audio_complete
    public string? LanguageCode { get; set; }
    public int? DurationSeconds { get; set; }
}
