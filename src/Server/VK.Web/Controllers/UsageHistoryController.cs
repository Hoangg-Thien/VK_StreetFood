using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class UsageHistoryController : AdminBaseController
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<UsageHistoryController> _logger;

    public UsageHistoryController(VKStreetFoodDbContext context, ILogger<UsageHistoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? language, int? poiId, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        try
        {
            var query = _context.VisitLogs
                .Include(v => v.Tourist)
                .Include(v => v.PointOfInterest)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(language))
                query = query.Where(v => v.LanguageUsed == language);

            if (poiId.HasValue)
                query = query.Where(v => v.PointOfInterestId == poiId);

            if (fromDate.HasValue)
                query = query.Where(v => v.VisitedAt >= fromDate.Value.ToUniversalTime());

            if (toDate.HasValue)
                query = query.Where(v => v.VisitedAt <= toDate.Value.ToUniversalTime().AddDays(1));

            int pageSize = 15;
            int total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(v => v.VisitedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pois = await _context.PointsOfInterest.Select(p => new { p.Id, p.Name }).ToListAsync();

            // Analytics for chart
            var last7 = DateTime.UtcNow.AddDays(-7);
            var dailyVisits = await _context.VisitLogs
                .Where(v => v.VisitedAt >= last7)
                .GroupBy(v => v.VisitedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var langStats = await _context.VisitLogs
                .GroupBy(v => v.LanguageUsed)
                .Select(g => new { Language = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.Logs = logs;
            ViewBag.POIs = pois;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Language = language;
            ViewBag.DailyVisits = dailyVisits;
            ViewBag.LangStats = langStats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading usage history");
            ViewBag.Logs = new List<object>();
            ViewBag.POIs = new List<object>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 15;
            ViewBag.DailyVisits = new List<object>();
            ViewBag.LangStats = new List<object>();
        }

        return View("UsageHistoryPage");
    }
}
