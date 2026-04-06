using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.Web.Controllers;

public class UsageHistoryController : AdminBaseController
{
    private readonly IRepository<VisitLog> _visitLogRepository;
    private readonly IRepository<Analytics> _analyticsRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UsageHistoryController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UsageHistoryController(
        IRepository<VisitLog> visitLogRepository,
        IRepository<Analytics> analyticsRepository,
        IRepository<PointOfInterest> poiRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<UsageHistoryController> logger)
    {
        _visitLogRepository = visitLogRepository;
        _analyticsRepository = analyticsRepository;
        _poiRepository = poiRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? language, int? poiId, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        try
        {
            var safePage = Math.Max(1, page);
            var fromUtc = fromDate?.ToUniversalTime();
            var toExclusiveUtc = toDate?.ToUniversalTime().AddDays(1);

            var filteredQuery = _visitLogRepository.Query()
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(language))
                filteredQuery = filteredQuery.Where(v => v.LanguageUsed == language);

            if (poiId.HasValue)
                filteredQuery = filteredQuery.Where(v => v.PointOfInterestId == poiId.Value);

            if (fromUtc.HasValue)
                filteredQuery = filteredQuery.Where(v => v.VisitedAt >= fromUtc.Value);

            if (toExclusiveUtc.HasValue)
                filteredQuery = filteredQuery.Where(v => v.VisitedAt < toExclusiveUtc.Value);

            int pageSize = 15;
            int total = await filteredQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
            safePage = Math.Min(safePage, totalPages);

            var logs = await filteredQuery
                .Include(v => v.Tourist)
                .Include(v => v.PointOfInterest)
                .OrderByDescending(v => v.VisitedAt)
                .Skip((safePage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var analyticsQuery = _analyticsRepository.Query()
                .AsNoTracking()
                .Where(a => a.EventType == "audio_play");

            if (fromUtc.HasValue)
                analyticsQuery = analyticsQuery.Where(a => a.EventTimestamp >= fromUtc.Value);

            if (toExclusiveUtc.HasValue)
                analyticsQuery = analyticsQuery.Where(a => a.EventTimestamp < toExclusiveUtc.Value);

            if (!string.IsNullOrWhiteSpace(language))
                analyticsQuery = analyticsQuery.Where(a => a.LanguageCode == language);

            if (poiId.HasValue)
                analyticsQuery = analyticsQuery.Where(a => a.PointOfInterestId == poiId.Value);

            var audioPlayedCount = await analyticsQuery.CountAsync();
            var distinctLanguageCount = await filteredQuery
                .Where(v => !string.IsNullOrWhiteSpace(v.LanguageUsed))
                .Select(v => v.LanguageUsed)
                .Distinct()
                .CountAsync();
            var distinctPoiCount = await filteredQuery
                .Select(v => v.PointOfInterestId)
                .Distinct()
                .CountAsync();

            var pois = await _poiRepository.Query().Select(p => new { p.Id, p.Name }).ToListAsync();

            // Analytics for chart
            var chartFromUtc = fromUtc ?? DateTime.UtcNow.Date.AddDays(-6);
            var chartToExclusiveUtc = toExclusiveUtc ?? DateTime.UtcNow.Date.AddDays(1);

            var rawDailyVisits = await filteredQuery
                .Where(v => v.VisitedAt >= chartFromUtc && v.VisitedAt < chartToExclusiveUtc)
                .GroupBy(v => v.VisitedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var dailyVisits = new List<DailyVisitVm>();
            var startDate = chartFromUtc.Date;
            var endDate = chartToExclusiveUtc.Date.AddDays(-1);
            for (var day = startDate; day <= endDate; day = day.AddDays(1))
            {
                var hit = rawDailyVisits.FirstOrDefault(x => x.Date == day);
                dailyVisits.Add(new DailyVisitVm
                {
                    Date = day,
                    Count = hit?.Count ?? 0
                });
            }

            var langStats = await filteredQuery
                .Where(v => !string.IsNullOrWhiteSpace(v.LanguageUsed))
                .GroupBy(v => v.LanguageUsed)
                .Select(g => new { Language = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var analyticsFilterQuery = BuildAnalyticsQuery(fromDate, toDate, language, poiId);
            var topListened = await GetApiAsync<List<TopListenedPoiVm>>($"analytics/top-listened-pois{analyticsFilterQuery}take=10") ?? new();
            var avgListen = await GetApiAsync<List<AverageListenPoiVm>>($"analytics/avg-listen-per-poi{analyticsFilterQuery}take=10") ?? new();
            var heatmap = await GetApiAsync<List<HeatmapPointVm>>($"analytics/heatmap{analyticsFilterQuery}") ?? new();
            var routes = await GetApiAsync<List<AnonymousRouteVm>>($"analytics/anonymous-routes{analyticsFilterQuery}take=20") ?? new();

            ViewBag.Logs = logs;
            ViewBag.POIs = pois;
            ViewBag.Total = total;
            ViewBag.Page = safePage;
            ViewBag.PageSize = pageSize;
            ViewBag.Language = language;
            ViewBag.PoiId = poiId;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.AudioPlayedCount = audioPlayedCount;
            ViewBag.DistinctLanguageCount = distinctLanguageCount;
            ViewBag.DistinctPoiCount = distinctPoiCount;
            ViewBag.DailyVisits = dailyVisits;
            ViewBag.LangStats = langStats;
            ViewBag.TopListened = topListened;
            ViewBag.AvgListen = avgListen;
            ViewBag.Heatmap = heatmap;
            ViewBag.AnonymousRoutes = routes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading usage history");
            ViewBag.Logs = new List<object>();
            ViewBag.POIs = new List<object>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 15;
            ViewBag.Language = language;
            ViewBag.PoiId = poiId;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.AudioPlayedCount = 0;
            ViewBag.DistinctLanguageCount = 0;
            ViewBag.DistinctPoiCount = 0;
            ViewBag.DailyVisits = new List<object>();
            ViewBag.LangStats = new List<object>();
            ViewBag.TopListened = new List<TopListenedPoiVm>();
            ViewBag.AvgListen = new List<AverageListenPoiVm>();
            ViewBag.Heatmap = new List<HeatmapPointVm>();
            ViewBag.AnonymousRoutes = new List<AnonymousRouteVm>();
        }

        return View("UsageHistoryPage");
    }

    public class DailyVisitVm
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    private async Task<T?> GetApiAsync<T>(string relativeUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("VKAPI");
            using var response = await client.GetAsync(relativeUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("VKAPI call failed: {Url} ({Status})", relativeUrl, response.StatusCode);
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VKAPI call error: {Url}", relativeUrl);
            return default;
        }
    }

    private static string BuildAnalyticsQuery(DateTime? fromDate, DateTime? toDate, string? languageCode, int? poiId)
    {
        var parts = new List<string>();
        if (fromDate.HasValue)
            parts.Add($"from={Uri.EscapeDataString(fromDate.Value.Date.ToUniversalTime().ToString("O"))}");
        if (toDate.HasValue)
            parts.Add($"to={Uri.EscapeDataString(toDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime().ToString("O"))}");
        if (!string.IsNullOrWhiteSpace(languageCode))
            parts.Add($"languageCode={Uri.EscapeDataString(languageCode)}");
        if (poiId.HasValue)
            parts.Add($"poiId={poiId.Value}");

        return parts.Count == 0 ? "?" : "?" + string.Join("&", parts) + "&";
    }

    public class TopListenedPoiVm
    {
        public int POIId { get; set; }
        public string POIName { get; set; } = string.Empty;
        public int AudioPlayCount { get; set; }
        public int AudioCompleteCount { get; set; }
        public int UniqueListeners { get; set; }
        public double CompletionRate { get; set; }
    }

    public class AverageListenPoiVm
    {
        public int POIId { get; set; }
        public string POIName { get; set; } = string.Empty;
        public double AverageDurationSeconds { get; set; }
        public int SampleCount { get; set; }
    }

    public class HeatmapPointVm
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Weight { get; set; }
    }

    public class AnonymousRoutePointVm
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime VisitedAt { get; set; }
    }

    public class AnonymousRouteVm
    {
        public string AnonymousVisitorId { get; set; } = string.Empty;
        public int PointCount { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public List<AnonymousRoutePointVm> Points { get; set; } = new();
    }
}
