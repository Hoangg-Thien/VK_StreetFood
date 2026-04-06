using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.Web.Controllers;

public class OwnerController : OwnerBaseController
{
    private readonly IRepository<AudioContent> _audioRepository;
    private readonly IRepository<PoiContentChangeRequest> _changeRequestRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OwnerController> _logger;

    public OwnerController(
        IRepository<AudioContent> audioRepository,
        IRepository<PoiContentChangeRequest> changeRequestRepository,
        IRepository<User> userRepository,
        IRepository<Vendor> vendorRepository,
        IUnitOfWork unitOfWork,
        ILogger<OwnerController> logger)
    {
        _audioRepository = audioRepository;
        _changeRequestRepository = changeRequestRepository;
        _userRepository = userRepository;
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var ownerCtx = await GetOwnerContextAsync();
        if (ownerCtx == null)
            return RedirectToAction("Index", "Home");

        var audios = await _audioRepository.Query()
            .Where(a => a.PointOfInterestId == ownerCtx.PointOfInterest.Id)
            .OrderBy(a => a.LanguageCode)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        var requests = await _changeRequestRepository.Query()
            .Include(r => r.AudioContent)
            .Where(r => r.VendorId == ownerCtx.Vendor.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.Vendor = ownerCtx.Vendor;
        ViewBag.Poi = ownerCtx.PointOfInterest;
        ViewBag.Audios = audios;
        ViewBag.ChangeRequests = requests;
        return View("OwnerPage");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTranslationChange(
        string languageCode,
        string textContent,
        int? audioContentId,
        string actionType = "update")
    {
        return await SubmitChangeRequestAsync("translation", languageCode, textContent, audioContentId, actionType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAudioChange(
        string languageCode,
        string textContent,
        int? audioContentId,
        string actionType = "update")
    {
        return await SubmitChangeRequestAsync("audio", languageCode, textContent, audioContentId, actionType);
    }

    private async Task<IActionResult> SubmitChangeRequestAsync(
        string requestType,
        string languageCode,
        string textContent,
        int? audioContentId,
        string actionType)
    {
        try
        {
            var ownerCtx = await GetOwnerContextAsync();
            if (ownerCtx == null)
            {
                TempData["Error"] = "Phiên đăng nhập không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(languageCode) || string.IsNullOrWhiteSpace(textContent))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ ngôn ngữ và nội dung.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedAction = string.IsNullOrWhiteSpace(actionType) ? "update" : actionType.Trim().ToLowerInvariant();
            if (normalizedAction != "create" && normalizedAction != "update")
                normalizedAction = "update";

            AudioContent? targetAudio = null;
            if (audioContentId.HasValue && audioContentId.Value > 0)
            {
                targetAudio = await _audioRepository.Query()
                    .FirstOrDefaultAsync(a => a.Id == audioContentId.Value && a.PointOfInterestId == ownerCtx.PointOfInterest.Id);

                if (targetAudio == null)
                {
                    TempData["Error"] = "Audio mục tiêu không thuộc POI của bạn.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var request = new PoiContentChangeRequest
            {
                OwnerUserId = ownerCtx.User.Id,
                VendorId = ownerCtx.Vendor.Id,
                PointOfInterestId = ownerCtx.PointOfInterest.Id,
                RequestType = requestType,
                ActionType = normalizedAction,
                AudioContentId = targetAudio?.Id,
                LanguageCode = languageCode.Trim().ToLowerInvariant(),
                TextContent = textContent.Trim(),
                Status = "pending"
            };

            await _changeRequestRepository.AddAsync(request);
            await _unitOfWork.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu chỉnh sửa. Admin sẽ duyệt trước khi áp dụng.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Owner submit change request failed");
            TempData["Error"] = "Không thể gửi yêu cầu lúc này.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<OwnerContext?> GetOwnerContextAsync()
    {
        var vendorId = HttpContext.Session.GetInt32("VendorId");
        var email = HttpContext.Session.GetString("UserEmail");

        if (!vendorId.HasValue || string.IsNullOrWhiteSpace(email))
            return null;

        var user = await _userRepository.Query()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email == email && u.Role == "poi_owner");

        if (user == null)
            return null;

        var vendor = await _vendorRepository.Query()
            .Include(v => v.PointOfInterest)
            .FirstOrDefaultAsync(v => v.Id == vendorId.Value && !v.IsDeleted);

        if (vendor?.PointOfInterest == null)
            return null;

        return new OwnerContext(user, vendor, vendor.PointOfInterest);
    }

    private sealed record OwnerContext(User User, Vendor Vendor, PointOfInterest PointOfInterest);
}
