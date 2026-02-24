using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;

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
            var analyticsEvent = new Analytics
            {
                TouristId = request.TouristId,
                PointOfInterestId = request.PoiId,
                EventType = request.EventType,
                DeviceInfo = request.DeviceInfo,
                LanguageCode = request.LanguageCode ?? "vi",
                DurationSeconds = request.DurationSeconds,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            _context.Analytics.Add(analyticsEvent);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Analytics event recorded: {EventType} for POI {PoiId} by Tourist {TouristId}",
                request.EventType, request.PoiId, request.TouristId);

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
}

public class RecordEventRequest
{
    public int? TouristId { get; set; }
    public int PoiId { get; set; }
    public string EventType { get; set; } = string.Empty; // view, qr_scan, audio_play, audio_complete
    public string? DeviceInfo { get; set; }
    public string? LanguageCode { get; set; }
    public int? DurationSeconds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
