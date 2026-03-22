using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Entities;

namespace VK.Web.Controllers;

public class AudioController : Controller
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<AudioController> _logger;

    public AudioController(VKStreetFoodDbContext context, ILogger<AudioController> logger)
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

    public async Task<IActionResult> Index(string? search, string? language, int? poiId, int page = 1)
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

            var query = _context.AudioContents
                .Include(a => a.PointOfInterest)
                .AsQueryable();

            if (isOwner && ownerPoiId.HasValue)
                query = query.Where(a => a.PointOfInterestId == ownerPoiId.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => a.PointOfInterest.Name.Contains(search) || a.TextContent.Contains(search));

            if (!string.IsNullOrWhiteSpace(language))
                query = query.Where(a => a.LanguageCode == language);

            if (!isOwner && poiId.HasValue)
                query = query.Where(a => a.PointOfInterestId == poiId);

            int pageSize = 10;
            int total = await query.CountAsync();
            var audios = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var poisQuery = _context.PointsOfInterest.AsQueryable();
            if (isOwner && ownerPoiId.HasValue)
                poisQuery = poisQuery.Where(p => p.Id == ownerPoiId.Value);

            var pois = await poisQuery.Select(p => new { p.Id, p.Name }).ToListAsync();

            ViewBag.Audios = audios;
            ViewBag.POIs = pois;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Language = language;
            ViewBag.IsOwner = isOwner;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audio list");
            ViewBag.Audios = new List<AudioContent>();
            ViewBag.POIs = new List<object>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 10;
            ViewBag.IsOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        }

        return View("AudioPage");
    }

    [HttpPost]
    public async Task<IActionResult> Create(AudioContent model)
    {
        var isOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (isOwner)
            {
                var ownerData = await GetOwnerContextAsync();
                if (!ownerData.HasValue || ownerData.Value.PointOfInterestId != model.PointOfInterestId)
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
                    RequestType = "audio",
                    ActionType = "create",
                    AudioContentId = null,
                    LanguageCode = model.LanguageCode,
                    TextContent = model.TextContent,
                    Status = "pending"
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã gửi yêu cầu thêm audio. Chờ admin duyệt.";
                return RedirectToAction(nameof(Index));
            }

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
        var isOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        try
        {
            var existing = await _context.AudioContents.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Không tìm thấy audio.";
                return RedirectToAction(nameof(Index));
            }

            if (isOwner)
            {
                var ownerData = await GetOwnerContextAsync();
                if (!ownerData.HasValue || ownerData.Value.PointOfInterestId != existing.PointOfInterestId)
                {
                    TempData["Error"] = "Bạn chỉ có thể gửi yêu cầu cho audio của POI mình.";
                    return RedirectToAction(nameof(Index));
                }

                var owner = ownerData.Value;

                _context.PoiContentChangeRequests.Add(new PoiContentChangeRequest
                {
                    OwnerUserId = owner.UserId,
                    VendorId = owner.VendorId,
                    PointOfInterestId = owner.PointOfInterestId,
                    RequestType = "audio",
                    ActionType = "update",
                    AudioContentId = existing.Id,
                    LanguageCode = model.LanguageCode,
                    TextContent = model.TextContent,
                    Status = "pending"
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã gửi yêu cầu cập nhật audio. Chờ admin duyệt.";
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
                        TempData["Error"] = "Bạn chỉ có thể gửi yêu cầu cho audio của POI mình.";
                        return RedirectToAction(nameof(Index));
                    }

                    var owner = ownerData.Value;

                    _context.PoiContentChangeRequests.Add(new PoiContentChangeRequest
                    {
                        OwnerUserId = owner.UserId,
                        VendorId = owner.VendorId,
                        PointOfInterestId = owner.PointOfInterestId,
                        RequestType = "audio",
                        ActionType = "delete",
                        AudioContentId = audio.Id,
                        LanguageCode = audio.LanguageCode,
                        TextContent = audio.TextContent,
                        Status = "pending"
                    });

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã gửi yêu cầu xóa audio. Chờ admin duyệt.";
                    return RedirectToAction(nameof(Index));
                }

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
}
