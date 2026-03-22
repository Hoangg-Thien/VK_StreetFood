using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VK.Core.Entities;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class OwnerContentApprovalController : AdminBaseController
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<OwnerContentApprovalController> _logger;

    public OwnerContentApprovalController(VKStreetFoodDbContext context, ILogger<OwnerContentApprovalController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string status = "pending")
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();

        var requests = await _context.PoiContentChangeRequests
            .Include(r => r.OwnerUser)
            .Include(r => r.Vendor)
            .Include(r => r.PointOfInterest)
            .Include(r => r.AudioContent)
            .Where(r => r.Status == normalizedStatus)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        ViewBag.Status = normalizedStatus;
        ViewBag.Requests = requests;
        return View("OwnerContentApprovalPage");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? reviewNote)
    {
        try
        {
            var request = await _context.PoiContentChangeRequests
                .Include(r => r.AudioContent)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != "pending")
            {
                TempData["Error"] = "Yêu cầu đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            await ApplyChangeRequestAsync(request);

            request.Status = "approved";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = reviewNote;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt và áp dụng thay đổi nội dung.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approve owner content request failed");
            TempData["Error"] = "Không thể duyệt yêu cầu.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reviewNote)
    {
        try
        {
            var request = await _context.PoiContentChangeRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            request.Status = "rejected";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = reviewNote;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã từ chối yêu cầu.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reject owner content request failed");
            TempData["Error"] = "Không thể từ chối yêu cầu.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ApplyChangeRequestAsync(PoiContentChangeRequest request)
    {
        if (string.Equals(request.RequestType, "poi", StringComparison.OrdinalIgnoreCase))
        {
            await ApplyPoiUpdateRequestAsync(request);
            return;
        }

        var normalizedAction = request.ActionType.Trim().ToLowerInvariant();

        if (normalizedAction == "delete" && request.AudioContentId.HasValue)
        {
            var toDelete = await _context.AudioContents
                .FirstOrDefaultAsync(a => a.Id == request.AudioContentId.Value && a.PointOfInterestId == request.PointOfInterestId);

            if (toDelete != null)
                toDelete.IsDeleted = true;

            return;
        }

        if (normalizedAction == "update" && request.AudioContentId.HasValue)
        {
            var audio = await _context.AudioContents
                .FirstOrDefaultAsync(a => a.Id == request.AudioContentId.Value && a.PointOfInterestId == request.PointOfInterestId);

            if (audio != null)
            {
                audio.LanguageCode = request.LanguageCode;
                audio.TextContent = request.TextContent;
                return;
            }
        }

        if (normalizedAction == "create" || !request.AudioContentId.HasValue)
        {
            var existingByLang = await _context.AudioContents
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == request.PointOfInterestId &&
                    a.LanguageCode == request.LanguageCode);

            if (existingByLang != null)
            {
                existingByLang.TextContent = request.TextContent;
                request.AudioContentId = existingByLang.Id;
                return;
            }

            var audio = new AudioContent
            {
                PointOfInterestId = request.PointOfInterestId,
                LanguageCode = request.LanguageCode,
                TextContent = request.TextContent,
                IsGenerated = false,
                AudioFileUrl = null,
                DurationSeconds = null
            };

            _context.AudioContents.Add(audio);
            await _context.SaveChangesAsync();
            request.AudioContentId = audio.Id;
        }
    }

    private async Task ApplyPoiUpdateRequestAsync(PoiContentChangeRequest request)
    {
        PoiEditPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PoiEditPayload>(request.TextContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Nội dung yêu cầu POI không hợp lệ.", ex);
        }

        if (payload == null)
            throw new InvalidOperationException("Không đọc được dữ liệu POI từ yêu cầu.");

        var poi = await _context.PointsOfInterest
            .FirstOrDefaultAsync(p => p.Id == request.PointOfInterestId);

        if (poi == null)
            throw new InvalidOperationException("Không tìm thấy POI để áp dụng yêu cầu.");

        poi.Name = payload.Name;
        poi.Description = payload.Description;
        poi.Address = payload.Address;
        poi.Latitude = payload.Latitude;
        poi.Longitude = payload.Longitude;
        poi.IsActive = payload.IsActive;
        poi.CategoryId = payload.CategoryId;
        poi.ImageUrl = payload.ImageUrl;
    }

    private sealed class PoiEditPayload
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsActive { get; set; }
        public int? CategoryId { get; set; }
        public string? ImageUrl { get; set; }
    }
}
