using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;

namespace VK.Web.Controllers;

public class TranslationController : Controller
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(VKStreetFoodDbContext context, ILogger<TranslationController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        var isLoggedIn = HttpContext.Session.GetString("UserLoggedIn") == "true";
        var role = HttpContext.Session.GetString("UserRole") ?? string.Empty;
        var isAllowedRole = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(role, "poi_owner", StringComparison.OrdinalIgnoreCase);

        if (!isLoggedIn || !isAllowedRole)
        {
            context.Result = new RedirectToActionResult("Index", "Home", null);
            return;
        }

        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index(string? search, int? poiId, int page = 1)
    {
        try
        {
            var isOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
            var ownerPoiId = isOwner ? await GetOwnerPoiIdAsync() : null;
            if (isOwner && !ownerPoiId.HasValue)
            {
                TempData["Error"] = "Không xác định được POI của chủ quán.";
                return RedirectToAction("Index", "Owner");
            }

            // Group audio contents by POI to show translation completeness
            var poisQuery = _context.PointsOfInterest
                .Include(p => p.AudioContents)
                .AsQueryable();

            if (isOwner && ownerPoiId.HasValue)
                poisQuery = poisQuery.Where(p => p.Id == ownerPoiId.Value);

            if (!string.IsNullOrWhiteSpace(search))
                poisQuery = poisQuery.Where(p => p.Name.Contains(search));

            if (!isOwner && poiId.HasValue)
                poisQuery = poisQuery.Where(p => p.Id == poiId);

            int pageSize = 10;
            int total = await poisQuery.CountAsync();
            var pois = await poisQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var allPoisQuery = _context.PointsOfInterest.AsQueryable();
            if (isOwner && ownerPoiId.HasValue)
                allPoisQuery = allPoisQuery.Where(p => p.Id == ownerPoiId.Value);

            var allPois = await allPoisQuery.Select(p => new { p.Id, p.Name }).ToListAsync();

            ViewBag.POIs = pois;
            ViewBag.AllPOIs = allPois;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.IsOwner = isOwner;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading translations");
            ViewBag.POIs = new List<PointOfInterest>();
            ViewBag.AllPOIs = new List<object>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 10;
            ViewBag.IsOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        }

        return View("TranslationPage");
    }

    [HttpPost]
    public async Task<IActionResult> SaveTranslation(int poiId, string languageCode, string textContent, int? existingId)
    {
        var isOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (isOwner)
            {
                var ownerData = await GetOwnerContextAsync();
                if (!ownerData.HasValue || ownerData.Value.PointOfInterestId != poiId)
                {
                    TempData["Error"] = "Bạn chỉ có thể gửi yêu cầu cho POI của mình.";
                    return RedirectToAction(nameof(Index));
                }

                var owner = ownerData.Value;

                _context.PoiContentChangeRequests.Add(new PoiContentChangeRequest
                {
                    OwnerUserId = owner.UserId,
                    VendorId = owner.VendorId,
                    PointOfInterestId = owner.PointOfInterestId,
                    RequestType = "translation",
                    ActionType = existingId.HasValue && existingId > 0 ? "update" : "create",
                    AudioContentId = existingId.HasValue && existingId > 0 ? existingId.Value : null,
                    LanguageCode = languageCode,
                    TextContent = textContent,
                    Status = "pending"
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã gửi yêu cầu bản dịch [{languageCode.ToUpper()}]. Chờ admin duyệt.";
                return RedirectToAction(nameof(Index));
            }

            if (existingId.HasValue && existingId > 0)
            {
                var existing = await _context.AudioContents.FindAsync(existingId.Value);
                if (existing != null)
                {
                    existing.LanguageCode = languageCode.Trim().ToLowerInvariant();
                    existing.TextContent = textContent;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật bản dịch [{languageCode.ToUpper()}] thành công!";
                    return RedirectToAction(nameof(Index));
                }
            }

            var normalizedLanguageCode = languageCode.Trim().ToLowerInvariant();
            var existingAny = await _context.AudioContents
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == poiId &&
                    a.LanguageCode == normalizedLanguageCode);

            if (existingAny != null)
            {
                existingAny.TextContent = textContent;
                existingAny.LanguageCode = normalizedLanguageCode;
                existingAny.IsDeleted = false;
                existingAny.DeletedAt = null;
                existingAny.IsGenerated = false;
                existingAny.AudioFileUrl = null;
                existingAny.DurationSeconds = null;

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã khôi phục bản dịch [{normalizedLanguageCode.ToUpper()}] thành công!";
                return RedirectToAction(nameof(Index));
            }

            var newAudio = new AudioContent
            {
                PointOfInterestId = poiId,
                LanguageCode = normalizedLanguageCode,
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
        var isOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        try
        {
            var audio = await _context.AudioContents.FindAsync(id);
            if (audio != null)
            {
                if (isOwner)
                {
                    var ownerData = await GetOwnerContextAsync();
                    if (!ownerData.HasValue || ownerData.Value.PointOfInterestId != audio.PointOfInterestId)
                    {
                        TempData["Error"] = "Bạn chỉ có thể gửi yêu cầu cho POI của mình.";
                        return RedirectToAction(nameof(Index));
                    }

                    var owner = ownerData.Value;

                    _context.PoiContentChangeRequests.Add(new PoiContentChangeRequest
                    {
                        OwnerUserId = owner.UserId,
                        VendorId = owner.VendorId,
                        PointOfInterestId = owner.PointOfInterestId,
                        RequestType = "translation",
                        ActionType = "delete",
                        AudioContentId = audio.Id,
                        LanguageCode = audio.LanguageCode,
                        TextContent = audio.TextContent,
                        Status = "pending"
                    });

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã gửi yêu cầu xóa bản dịch. Chờ admin duyệt.";
                    return RedirectToAction(nameof(Index));
                }

                DeleteAudioFileIfExists(audio.AudioFileUrl);
                _context.AudioContents.Remove(audio);
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

    private async Task<int?> GetOwnerPoiIdAsync()
    {
        var vendorId = HttpContext.Session.GetInt32("VendorId");
        if (!vendorId.HasValue)
            return null;

        return await _context.Vendors
            .Where(v => v.Id == vendorId.Value && !v.IsDeleted)
            .Select(v => (int?)v.PointOfInterestId)
            .FirstOrDefaultAsync();
    }

    private async Task<(int UserId, int VendorId, int PointOfInterestId)?> GetOwnerContextAsync()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        var vendorId = HttpContext.Session.GetInt32("VendorId");
        if (string.IsNullOrWhiteSpace(email) || !vendorId.HasValue)
            return null;

        var userId = await _context.Users
            .Where(u => !u.IsDeleted && u.Email == email && u.Role == "poi_owner")
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        var poiId = await _context.Vendors
            .Where(v => !v.IsDeleted && v.Id == vendorId.Value)
            .Select(v => (int?)v.PointOfInterestId)
            .FirstOrDefaultAsync();

        if (!userId.HasValue || !poiId.HasValue)
            return null;

        return (userId.Value, vendorId.Value, poiId.Value);
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
