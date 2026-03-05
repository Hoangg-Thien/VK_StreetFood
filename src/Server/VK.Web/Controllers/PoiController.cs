using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;

namespace VK.Web.Controllers;

public class PoiController : AdminBaseController
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<PoiController> _logger;

    public PoiController(VKStreetFoodDbContext context, ILogger<PoiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId, bool? isActive, int page = 1)
    {
        try
        {
            var query = _context.PointsOfInterest
                .Include(p => p.Category)
                .Include(p => p.AudioContents)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search) || p.Address.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);

            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            int pageSize = 10;
            int total = await query.CountAsync();
            var pois = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.Categories.ToListAsync();

            ViewBag.POIs = pois;
            ViewBag.Categories = categories;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.IsActive = isActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POI list");
            ViewBag.POIs = new List<PointOfInterest>();
            ViewBag.Categories = new List<Category>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 10;
        }

        return View("PoiPage");
    }

    [HttpPost]
    public async Task<IActionResult> Create(PointOfInterest model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            _context.PointsOfInterest.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Thêm địa điểm thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating POI");
            TempData["Error"] = "Có lỗi xảy ra khi thêm địa điểm.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PointOfInterest model)
    {
        try
        {
            var existing = await _context.PointsOfInterest.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Không tìm thấy địa điểm.";
                return RedirectToAction(nameof(Index));
            }

            existing.Name = model.Name;
            existing.Description = model.Description;
            existing.Address = model.Address;
            existing.Latitude = model.Latitude;
            existing.Longitude = model.Longitude;
            existing.IsActive = model.IsActive;
            existing.CategoryId = model.CategoryId;
            existing.ImageUrl = model.ImageUrl;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật địa điểm thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing POI");
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var poi = await _context.PointsOfInterest.FindAsync(id);
            if (poi != null)
            {
                poi.IsDeleted = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa địa điểm thành công!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting POI");
            TempData["Error"] = "Có lỗi xảy ra khi xóa.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var poi = await _context.PointsOfInterest.FindAsync(id);
        if (poi != null)
        {
            poi.IsActive = !poi.IsActive;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
