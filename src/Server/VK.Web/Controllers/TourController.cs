using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class TourController : Controller
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<TourController> _logger;

    public TourController(VKStreetFoodDbContext context, ILogger<TourController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Load POIs to build tours from
            var pois = await _context.PointsOfInterest
                .Include(p => p.Category)
                .Include(p => p.AudioContents)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var categories = await _context.Categories.ToListAsync();

            ViewBag.POIs = pois;
            ViewBag.Categories = categories;
            ViewBag.TotalPOIs = pois.Count;
            ViewBag.TotalAudio = pois.Sum(p => p.AudioContents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tour data");
            ViewBag.POIs = new List<object>();
            ViewBag.Categories = new List<object>();
            ViewBag.TotalPOIs = 0;
            ViewBag.TotalAudio = 0;
        }

        return View("TourPage");
    }
}
