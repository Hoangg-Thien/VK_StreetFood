using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;

namespace VK.Web.Controllers;

public class TranslationController : AdminBaseController
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(VKStreetFoodDbContext context, ILogger<TranslationController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, int? poiId, int page = 1)
    {
        try
        {
            // Group audio contents by POI to show translation completeness
            var poisQuery = _context.PointsOfInterest
                .Include(p => p.AudioContents)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                poisQuery = poisQuery.Where(p => p.Name.Contains(search));

            if (poiId.HasValue)
                poisQuery = poisQuery.Where(p => p.Id == poiId);

            int pageSize = 10;
            int total = await poisQuery.CountAsync();
            var pois = await poisQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var allPois = await _context.PointsOfInterest.Select(p => new { p.Id, p.Name }).ToListAsync();

            ViewBag.POIs = pois;
            ViewBag.AllPOIs = allPois;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading translations");
            ViewBag.POIs = new List<PointOfInterest>();
            ViewBag.AllPOIs = new List<object>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 10;
        }

        return View("TranslationPage");
    }

    [HttpPost]
    public async Task<IActionResult> SaveTranslation(int poiId, string languageCode, string textContent, int? existingId)
    {
        try
        {
            if (existingId.HasValue && existingId > 0)
            {
                var existing = await _context.AudioContents.FindAsync(existingId.Value);
                if (existing != null)
                {
                    existing.TextContent = textContent;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật bản dịch [{languageCode.ToUpper()}] thành công!";
                    return RedirectToAction(nameof(Index));
                }
            }

            var newAudio = new AudioContent
            {
                PointOfInterestId = poiId,
                LanguageCode = languageCode,
                TextContent = textContent
            };
            _context.AudioContents.Add(newAudio);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Thêm bản dịch [{languageCode.ToUpper()}] thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving translation");
            TempData["Error"] = "Có lỗi xảy ra khi lưu bản dịch.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTranslation(int id)
    {
        try
        {
            var audio = await _context.AudioContents.FindAsync(id);
            if (audio != null)
            {
                audio.IsDeleted = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa bản dịch thành công!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting translation");
            TempData["Error"] = "Có lỗi xảy ra.";
        }
        return RedirectToAction(nameof(Index));
    }
}
