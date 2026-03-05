using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;

namespace VK.Web.Controllers;

public class AudioController : AdminBaseController
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<AudioController> _logger;

    public AudioController(VKStreetFoodDbContext context, ILogger<AudioController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, string? language, int? poiId, int page = 1)
    {
        try
        {
            var query = _context.AudioContents
                .Include(a => a.PointOfInterest)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => a.PointOfInterest.Name.Contains(search) || a.TextContent.Contains(search));

            if (!string.IsNullOrWhiteSpace(language))
                query = query.Where(a => a.LanguageCode == language);

            if (poiId.HasValue)
                query = query.Where(a => a.PointOfInterestId == poiId);

            int pageSize = 10;
            int total = await query.CountAsync();
            var audios = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pois = await _context.PointsOfInterest.Select(p => new { p.Id, p.Name }).ToListAsync();

            ViewBag.Audios = audios;
            ViewBag.POIs = pois;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Language = language;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audio list");
            ViewBag.Audios = new List<AudioContent>();
            ViewBag.POIs = new List<object>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 10;
        }

        return View("AudioPage");
    }

    [HttpPost]
    public async Task<IActionResult> Create(AudioContent model)
    {
        try
        {
            _context.AudioContents.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Thêm audio thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audio");
            TempData["Error"] = "Có lỗi xảy ra khi thêm audio.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(AudioContent model)
    {
        try
        {
            var existing = await _context.AudioContents.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Không tìm thấy audio.";
                return RedirectToAction(nameof(Index));
            }

            existing.LanguageCode = model.LanguageCode;
            existing.TextContent = model.TextContent;
            existing.PointOfInterestId = model.PointOfInterestId;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật audio thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing audio");
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var audio = await _context.AudioContents.FindAsync(id);
            if (audio != null)
            {
                audio.IsDeleted = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa audio thành công!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting audio");
            TempData["Error"] = "Có lỗi xảy ra khi xóa.";
        }
        return RedirectToAction(nameof(Index));
    }
}
