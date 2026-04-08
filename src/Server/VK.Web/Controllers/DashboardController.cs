using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.Web.Controllers;

public class DashboardController : AdminBaseController
{
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<Tourist> _touristRepository;
    private readonly IRepository<VisitLog> _visitLogRepository;
    private readonly IRepository<AudioContent> _audioRepository;
    private readonly IRepository<Rating> _ratingRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IRepository<PointOfInterest> poiRepository,
        IRepository<Tourist> touristRepository,
        IRepository<VisitLog> visitLogRepository,
        IRepository<AudioContent> audioRepository,
        IRepository<Rating> ratingRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<DashboardController> logger)
    {
        _poiRepository = poiRepository;
        _touristRepository = touristRepository;
        _visitLogRepository = visitLogRepository;
        _audioRepository = audioRepository;
        _ratingRepository = ratingRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var totalPOIs = await _poiRepository.Query().CountAsync();
            var totalTourists = await _touristRepository.Query().CountAsync();
            var totalVisits = await _visitLogRepository.Query().CountAsync();
            var totalAudio = await _audioRepository.Query().CountAsync();

            var averageRating = await _ratingRepository.Query()
                .AverageAsync(r => (double?)r.Score) ?? 0;

            // Recent POIs
            var recentPOIs = await _poiRepository.Query()
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Last 7 days visit chart data
            var last7Days = DateTime.UtcNow.AddDays(-7);
            var dailyVisits = await _visitLogRepository.Query()
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
