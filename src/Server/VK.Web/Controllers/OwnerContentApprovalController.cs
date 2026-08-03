using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.Web.Controllers;

public class OwnerContentApprovalController : AdminBaseController
{
    private readonly IRepository<PoiContentChangeRequest> _changeRequestRepository;
    private readonly IRepository<AudioContent> _audioRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OwnerContentApprovalController> _logger;

    public OwnerContentApprovalController(
        IRepository<PoiContentChangeRequest> changeRequestRepository,
        IRepository<AudioContent> audioRepository,
        IRepository<PointOfInterest> poiRepository,
        IUnitOfWork unitOfWork,
        ILogger<OwnerContentApprovalController> logger)
    {
        _changeRequestRepository = changeRequestRepository;
        _audioRepository = audioRepository;
        _poiRepository = poiRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string status = "pending")
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();

        var requests = await _changeRequestRepository.Query()
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
            var request = await _changeRequestRepository.Query()
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

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await ApplyChangeRequestAsync(request);

                request.Status = "approved";
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewNote = reviewNote;

                await _unitOfWork.SaveChangesAsync();
            });

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
            var request = await _changeRequestRepository.Query().FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            request.Status = "rejected";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = reviewNote;

            await _unitOfWork.SaveChangesAsync();
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
            var toDelete = await _audioRepository.Query()
                .FirstOrDefaultAsync(a => a.Id == request.AudioContentId.Value && a.PointOfInterestId == request.PointOfInterestId);

            if (toDelete != null)
            {
                DeleteAudioFileIfExists(toDelete.AudioFileUrl);
                _audioRepository.Remove(toDelete);
            }

            return;
        }

        if (normalizedAction == "update" && request.AudioContentId.HasValue)
        {
            var audio = await _audioRepository.Query()
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
            var normalizedLanguageCode = request.LanguageCode.Trim().ToLowerInvariant();

            var existingByLang = await _audioRepository.Query()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == request.PointOfInterestId &&
                    a.LanguageCode == normalizedLanguageCode);

            if (existingByLang != null)
            {
                existingByLang.LanguageCode = normalizedLanguageCode;
                existingByLang.TextContent = request.TextContent;
                existingByLang.IsDeleted = false;
                existingByLang.DeletedAt = null;
                existingByLang.IsGenerated = false;
                existingByLang.AudioFileUrl = null;
                existingByLang.DurationSeconds = null;
                request.AudioContentId = existingByLang.Id;
                return;
            }

            var audio = new AudioContent
            {
                PointOfInterestId = request.PointOfInterestId,
                LanguageCode = normalizedLanguageCode,
                TextContent = request.TextContent,
                IsGenerated = false,
                AudioFileUrl = null,
                DurationSeconds = null
            };

            await _audioRepository.AddAsync(audio);
            request.AudioContent = audio;
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

        var poi = await _poiRepository.Query()
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

    private void DeleteAudioFileIfExists(string? audioFileUrl)
    {
        if (string.IsNullOrWhiteSpace(audioFileUrl))
            return;

        try
        {
            var apiStorageRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "VK.API", "App_Data"));

            var relativePath = audioFileUrl
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var fullPath = Path.GetFullPath(Path.Combine(apiStorageRoot, relativePath));
            if (!fullPath.StartsWith(apiStorageRoot, StringComparison.OrdinalIgnoreCase))
                return;

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete audio file {AudioFileUrl}", audioFileUrl);
        }
    }
}
