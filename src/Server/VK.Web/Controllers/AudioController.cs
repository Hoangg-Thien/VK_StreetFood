using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.Web.Controllers;

public class AudioController : Controller
{
    private readonly IRepository<AudioContent> _audioRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<PoiContentChangeRequest> _changeRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AudioController> _logger;

    public AudioController(
        IRepository<AudioContent> audioRepository,
        IRepository<PointOfInterest> poiRepository,
        IRepository<Vendor> vendorRepository,
        IRepository<User> userRepository,
        IRepository<PoiContentChangeRequest> changeRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<AudioController> logger)
    {
        _audioRepository = audioRepository;
        _poiRepository = poiRepository;
        _vendorRepository = vendorRepository;
        _userRepository = userRepository;
        _changeRequestRepository = changeRequestRepository;
        _unitOfWork = unitOfWork;
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

            var query = _audioRepository.Query()
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

            var poisQuery = _poiRepository.Query().AsQueryable();
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
            model.LanguageCode = model.LanguageCode.Trim().ToLowerInvariant();

            if (isOwner)
            {
                var ownerData = await GetOwnerContextAsync();
                if (!ownerData.HasValue || ownerData.Value.PointOfInterestId != model.PointOfInterestId)
                {
                    TempData["Error"] = "Bạn chỉ có thể gửi yêu cầu cho POI của mình.";
                    return RedirectToAction(nameof(Index));
                }

                var owner = ownerData.Value;

                await _changeRequestRepository.AddAsync(new PoiContentChangeRequest
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

                await _unitOfWork.SaveChangesAsync();
                TempData["Success"] = "Đã gửi yêu cầu thêm audio. Chờ admin duyệt.";
                return RedirectToAction(nameof(Index));
            }

            var existingAny = await _audioRepository.Query()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == model.PointOfInterestId &&
                    a.LanguageCode == model.LanguageCode);

            if (existingAny != null)
            {
                existingAny.TextContent = model.TextContent;
                existingAny.IsDeleted = false;
                existingAny.DeletedAt = null;
                existingAny.IsGenerated = false;
                existingAny.AudioFileUrl = null;
                existingAny.DurationSeconds = null;

                await _unitOfWork.SaveChangesAsync();
                TempData["Success"] = "Đã khôi phục audio cũ và cập nhật nội dung!";
                return RedirectToAction(nameof(Index));
            }

            await _audioRepository.AddAsync(model);
            await _unitOfWork.SaveChangesAsync();
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
            var existing = await _audioRepository.Query().FirstOrDefaultAsync(a => a.Id == model.Id);
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

                await _changeRequestRepository.AddAsync(new PoiContentChangeRequest
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

                await _unitOfWork.SaveChangesAsync();
                TempData["Success"] = "Đã gửi yêu cầu cập nhật audio. Chờ admin duyệt.";
                return RedirectToAction(nameof(Index));
            }

            existing.LanguageCode = model.LanguageCode;
            existing.TextContent = model.TextContent;
            existing.PointOfInterestId = model.PointOfInterestId;
            await _unitOfWork.SaveChangesAsync();
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
            var audio = await _audioRepository.Query().FirstOrDefaultAsync(a => a.Id == id);
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

                    await _changeRequestRepository.AddAsync(new PoiContentChangeRequest
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

                    await _unitOfWork.SaveChangesAsync();
                    TempData["Success"] = "Đã gửi yêu cầu xóa audio. Chờ admin duyệt.";
                    return RedirectToAction(nameof(Index));
                }

                DeleteAudioFileIfExists(audio.AudioFileUrl);
                _audioRepository.Remove(audio);
                await _unitOfWork.SaveChangesAsync();
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

        return await _vendorRepository.Query()
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

        var userId = await _userRepository.Query()
            .Where(u => !u.IsDeleted && u.Email == email && u.Role == "poi_owner")
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        var poiId = await _vendorRepository.Query()
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
            var apiWwwroot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "VK.API", "App_Data"));

            var relativePath = audioFileUrl
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var fullPath = Path.GetFullPath(Path.Combine(apiWwwroot, relativePath));
            if (!fullPath.StartsWith(apiWwwroot, StringComparison.OrdinalIgnoreCase))
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
