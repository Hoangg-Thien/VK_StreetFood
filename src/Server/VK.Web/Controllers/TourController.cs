using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class TourController : AdminBaseController
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
            var status = (Request.Query["status"].ToString() ?? string.Empty).Trim().ToLowerInvariant();

            var toursQuery = _context.Tours
                .Include(t => t.TourPoints.OrderBy(tp => tp.SortOrder))
                .ThenInclude(tp => tp.PointOfInterest)
                .AsQueryable();

            if (status is "active" or "draft" or "inactive")
            {
                toursQuery = toursQuery.Where(t => t.Status == status);
            }

            var tours = await toursQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Load POIs to build tours from
            var pois = await _context.PointsOfInterest
                .Include(p => p.Category)
                .Include(p => p.AudioContents)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var categories = await _context.Categories.ToListAsync();

            ViewBag.Tours = tours;
            ViewBag.POIs = pois;
            ViewBag.Categories = categories;
            ViewBag.TotalTours = tours.Count;
            ViewBag.TotalPOIs = pois.Count;
            ViewBag.TotalAudio = pois.Sum(p => p.AudioContents.Count);
            ViewBag.Status = status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tour data");
            ViewBag.Tours = new List<Tour>();
            ViewBag.POIs = new List<object>();
            ViewBag.Categories = new List<object>();
            ViewBag.TotalTours = 0;
            ViewBag.TotalPOIs = 0;
            ViewBag.TotalAudio = 0;
            ViewBag.Status = string.Empty;
        }

        return View("TourPage");
    }

    [HttpPost]
    public async Task<IActionResult> Create(TourUpsertInput input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "Tên tour là bắt buộc.";
                return RedirectToAction(nameof(Index));
            }

            var tour = new Tour
            {
                Name = input.Name.Trim(),
                Description = (input.Description ?? string.Empty).Trim(),
                Emoji = string.IsNullOrWhiteSpace(input.Emoji) ? "🍜" : input.Emoji.Trim(),
                EstimatedDurationMinutes = input.EstimatedDurationMinutes,
                Status = NormalizeStatus(input.Status)
            };

            _context.Tours.Add(tour);
            await _context.SaveChangesAsync();
            await SyncTourPointsAsync(tour.Id, input.PoiIds);

            TempData["Success"] = "Tạo tour thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tour");
            TempData["Error"] = "Có lỗi xảy ra khi tạo tour.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(TourUpsertInput input)
    {
        try
        {
            if (input.Id <= 0)
            {
                TempData["Error"] = "Tour không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "Tên tour là bắt buộc.";
                return RedirectToAction(nameof(Index));
            }

            var tour = await _context.Tours
                .Include(t => t.TourPoints)
                .FirstOrDefaultAsync(t => t.Id == input.Id);

            if (tour == null)
            {
                TempData["Error"] = "Không tìm thấy tour.";
                return RedirectToAction(nameof(Index));
            }

            tour.Name = input.Name.Trim();
            tour.Description = (input.Description ?? string.Empty).Trim();
            tour.Emoji = string.IsNullOrWhiteSpace(input.Emoji) ? "🍜" : input.Emoji.Trim();
            tour.EstimatedDurationMinutes = input.EstimatedDurationMinutes;
            tour.Status = NormalizeStatus(input.Status);

            await SyncTourPointsAsync(tour.Id, input.PoiIds);

            TempData["Success"] = "Cập nhật tour thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing tour");
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật tour.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var tour = await _context.Tours.FirstOrDefaultAsync(t => t.Id == id);
            if (tour == null)
            {
                TempData["Error"] = "Không tìm thấy tour.";
                return RedirectToAction(nameof(Index));
            }

            var tourPoints = await _context.TourPointsOfInterest
                .Where(tp => tp.TourId == id)
                .ToListAsync();

            foreach (var point in tourPoints)
            {
                point.IsDeleted = true;
                point.DeletedAt = DateTime.UtcNow;
            }

            tour.IsDeleted = true;
            tour.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Xóa tour thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tour");
            TempData["Error"] = "Có lỗi xảy ra khi xóa tour.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task SyncTourPointsAsync(int tourId, List<int>? poiIds)
    {
        var selectedPoiIds = (poiIds ?? new List<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var existing = await _context.TourPointsOfInterest
            .Where(tp => tp.TourId == tourId)
            .ToListAsync();

        foreach (var point in existing)
        {
            if (!selectedPoiIds.Contains(point.PointOfInterestId))
            {
                point.IsDeleted = true;
                point.DeletedAt = DateTime.UtcNow;
            }
        }

        for (var index = 0; index < selectedPoiIds.Count; index++)
        {
            var poiId = selectedPoiIds[index];
            var sortOrder = index + 1;

            var existed = existing.FirstOrDefault(tp => tp.PointOfInterestId == poiId);
            if (existed != null)
            {
                existed.IsDeleted = false;
                existed.DeletedAt = null;
                existed.SortOrder = sortOrder;
                continue;
            }

            _context.TourPointsOfInterest.Add(new TourPointOfInterest
            {
                TourId = tourId,
                PointOfInterestId = poiId,
                SortOrder = sortOrder
            });
        }

        await _context.SaveChangesAsync();
    }

    private static string NormalizeStatus(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "active" or "draft" or "inactive" ? value : "draft";
    }

    public sealed class TourUpsertInput
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Emoji { get; set; }
        public int? EstimatedDurationMinutes { get; set; }
        public string? Status { get; set; }
        public List<int>? PoiIds { get; set; }
    }
}
