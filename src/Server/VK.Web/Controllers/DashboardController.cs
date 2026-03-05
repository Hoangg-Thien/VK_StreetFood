using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class DashboardController : AdminBaseController
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
            var totalPOIs = await _context.PointsOfInterest.CountAsync();
            var totalTourists = await _context.Tourists.CountAsync();
            var totalVisits = await _context.VisitLogs.CountAsync();
            var totalAudio = await _context.AudioContents.CountAsync();

            var averageRating = await _context.Ratings
                .AverageAsync(r => (double?)r.Score) ?? 0;

            // Recent POIs
            var recentPOIs = await _context.PointsOfInterest
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Last 7 days visit chart data
            var last7Days = DateTime.UtcNow.AddDays(-7);
            var dailyVisits = await _context.VisitLogs
                .Where(v => v.VisitedAt >= last7Days)
                .GroupBy(v => v.VisitedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Fill missing days
            var chartData = new List<int>();
            var chartLabels = new List<string>();
            for (int d = 6; d >= 0; d--)
            {
                var day = DateTime.UtcNow.Date.AddDays(-d);
                var vh = dailyVisits.FirstOrDefault(x => x.Date == day);
                chartData.Add(vh?.Count ?? 0);
                chartLabels.Add(day.ToString("ddd", new System.Globalization.CultureInfo("vi-VN")));
            }

            ViewBag.TotalPOIs = totalPOIs;
            ViewBag.TotalTourists = totalTourists;
            ViewBag.TotalVisits = totalVisits;
            ViewBag.TotalAudio = totalAudio;
            ViewBag.AverageRating = Math.Round(averageRating, 2);
            ViewBag.RecentPOIs = recentPOIs;
            ViewBag.ChartDataJson = System.Text.Json.JsonSerializer.Serialize(chartData);
            ViewBag.ChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(chartLabels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            // Set defaults so the view still renders
            ViewBag.TotalPOIs = 0;
            ViewBag.TotalTourists = 0;
            ViewBag.TotalVisits = 0;
            ViewBag.TotalAudio = 0;
            ViewBag.AverageRating = 0.0;
            ViewBag.RecentPOIs = new List<object>();
            ViewBag.ChartDataJson = "[0,0,0,0,0,0,0]";
            ViewBag.ChartLabelsJson = "[\"T2\",\"T3\",\"T4\",\"T5\",\"T6\",\"T7\",\"CN\"]";
        }

        return View("DashboardPage");
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
