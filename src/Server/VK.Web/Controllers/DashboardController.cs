using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class DashboardController : Controller
{
    private readonly VKStreetFoodDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        VKStreetFoodDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Get statistics from database
            var totalPOIs = await _context.PointsOfInterest
                .Where(p => !p.IsDeleted)
                .CountAsync();

            var totalTourists = await _context.Tourists
                .Where(t => !t.IsDeleted)
                .CountAsync();

            var totalVisits = await _context.VisitLogs
                .Where(v => !v.IsDeleted)
                .CountAsync();

            var totalRatings = await _context.Ratings
                .Where(r => !r.IsDeleted)
                .CountAsync();

            var averageRating = await _context.Ratings
                .Where(r => !r.IsDeleted)
                .AverageAsync(r => (double?)r.RatingValue) ?? 0;

            // Recent visitors (last 7 days)
            var last7Days = DateTime.UtcNow.AddDays(-7);
            var recentVisitors = await _context.VisitLogs
                .Where(v => !v.IsDeleted && v.VisitedAt >= last7Days)
                .GroupBy(v => v.VisitedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Top 5 POIs by visits
            var topPOIs = await _context.VisitLogs
                .Where(v => !v.IsDeleted)
                .GroupBy(v => new { v.PointOfInterestId, v.PointOfInterest!.Name })
                .Select(g => new { POIName = g.Key.Name, VisitCount = g.Count() })
                .OrderByDescending(x => x.VisitCount)
                .Take(5)
                .ToListAsync();

            // Language distribution
            var languageStats = await _context.Tourists
                .Where(t => !t.IsDeleted)
                .GroupBy(t => t.PreferredLanguage)
                .Select(g => new { Language = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.TotalPOIs = totalPOIs;
            ViewBag.TotalTourists = totalTourists;
            ViewBag.TotalVisits = totalVisits;
            ViewBag.TotalRatings = totalRatings;
            ViewBag.AverageRating = Math.Round(averageRating, 2);
            ViewBag.RecentVisitors = recentVisitors;
            ViewBag.TopPOIs = topPOIs;
            ViewBag.LanguageStats = languageStats;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            return View("Error");
        }
    }

    public async Task<IActionResult> Analytics()
    {
        try
        {
            // Call API to get analytics
            var client = _httpClientFactory.CreateClient("VKAPI");
            var response = await client.GetAsync("analytics/dashboard");

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<dynamic>();
                ViewBag.AnalyticsData = data;
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics");
            return View("Error");
        }
    }
}
