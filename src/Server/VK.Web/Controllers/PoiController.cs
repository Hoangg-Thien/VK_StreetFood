using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Shared.Constants;
using VK.Web.Services;

namespace VK.Web.Controllers;

public class PoiController : Controller
{
    private readonly IPoiManagementRepository _poiManagementRepository;
    private readonly IRepository<PoiContentChangeRequest> _changeRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PoiController> _logger;
    private readonly ITextTranslationService _textTranslationService;
    private readonly IWebHostEnvironment _environment;

    public PoiController(
        IPoiManagementRepository poiManagementRepository,
        IRepository<PoiContentChangeRequest> changeRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<PoiController> logger,
        ITextTranslationService textTranslationService,
        IWebHostEnvironment environment)
    {
        _poiManagementRepository = poiManagementRepository;
        _changeRequestRepository = changeRequestRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _textTranslationService = textTranslationService;
        _environment = environment;
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

    public async Task<IActionResult> Index(string? search, int? categoryId, bool? isActive, int page = 1)
    {
        try
        {
            var role = HttpContext.Session.GetString("UserRole") ?? "admin";
            var isOwner = string.Equals(role, "poi_owner", StringComparison.OrdinalIgnoreCase);
            var ownerVendorId = HttpContext.Session.GetInt32("VendorId");

            int pageSize = 10;
            if (isOwner && !ownerVendorId.HasValue)
                return RedirectToAction("Index", "Home");

            var (pois, total) = await _poiManagementRepository.GetPagedPoisAsync(
                search,
                categoryId,
                isActive,
                isOwner ? ownerVendorId : null,
                page,
                pageSize);

            var categories = await _poiManagementRepository.GetCategoriesAsync();

            ViewBag.POIs = pois;
            ViewBag.Categories = categories;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.IsActive = isActive;
            ViewBag.IsOwner = isOwner;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POI list");
            ViewBag.POIs = new List<PointOfInterest>();
            ViewBag.Categories = new List<Category>();
            ViewBag.Total = 0;
            ViewBag.Page = 1;
            ViewBag.PageSize = 10;
            ViewBag.IsOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        }

        return View("PoiPage");
    }

    [HttpPost]
    public async Task<IActionResult> Create(PointOfInterest model)
    {
        if (string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Chủ quán không có quyền tạo quán mới.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            await _poiManagementRepository.AddPoiAsync(model);
            await _unitOfWork.SaveChangesAsync();

            await EnsureDefaultTranslationsAsync(
                model.Id,
                model.Name,
                model.Description,
                model.Address,
                updateVietnamese: true);

            await _unitOfWork.SaveChangesAsync();

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
        var isOwner = string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase);
        int? ownerVendorId = null;
        if (isOwner)
        {
            ownerVendorId = HttpContext.Session.GetInt32("VendorId");
            if (!ownerVendorId.HasValue)
            {
                TempData["Error"] = "Không xác định được quán quản lý.";
                return RedirectToAction(nameof(Index));
            }

            var ownerPoiId = await _poiManagementRepository.GetOwnerPoiIdAsync(ownerVendorId.Value);

            if (!ownerPoiId.HasValue || ownerPoiId.Value != model.Id)
            {
                TempData["Error"] = "Bạn chỉ có thể cập nhật quán của mình.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var ownerEmail = HttpContext.Session.GetString("UserEmail");
                if (string.IsNullOrWhiteSpace(ownerEmail))
                {
                    TempData["Error"] = "Không xác định được tài khoản chủ quán.";
                    return RedirectToAction(nameof(Index));
                }

                var ownerUser = await _poiManagementRepository.GetOwnerUserByEmailAsync(ownerEmail);

                if (ownerUser == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin chủ quán.";
                    return RedirectToAction(nameof(Index));
                }

                var payload = new PoiEditPayload
                {
                    Name = model.Name,
                    Description = model.Description,
                    Address = model.Address,
                    Latitude = model.Latitude,
                    Longitude = model.Longitude,
                    IsActive = model.IsActive,
                    CategoryId = model.CategoryId,
                    ImageUrl = model.ImageUrl
                };

                await _changeRequestRepository.AddAsync(new PoiContentChangeRequest
                {
                    OwnerUserId = ownerUser.Id,
                    VendorId = ownerVendorId.Value,
                    PointOfInterestId = model.Id,
                    RequestType = "poi",
                    ActionType = "update",
                    AudioContentId = null,
                    LanguageCode = "vi",
                    TextContent = JsonSerializer.Serialize(payload),
                    Status = "pending"
                });

                await _unitOfWork.SaveChangesAsync();
                TempData["Success"] = "Đã gửi yêu cầu chỉnh sửa POI. Admin duyệt xong mới áp dụng.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Owner submit POI edit request failed");
                TempData["Error"] = "Không thể gửi yêu cầu chỉnh sửa lúc này.";
            }

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var existing = await _poiManagementRepository.GetPoiByIdAsync(model.Id);
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

            await EnsureDefaultTranslationsAsync(
                existing.Id,
                model.Name,
                model.Description,
                model.Address,
                updateVietnamese: true);

            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = "Cập nhật địa điểm thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing POI");
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật.";
        }

        return RedirectToAction(nameof(Index));
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

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Chủ quán không có quyền xóa quán.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var poi = await _poiManagementRepository.GetPoiByIdAsync(id);
            if (poi == null)
            {
                TempData["Error"] = "Không tìm thấy địa điểm.";
                return RedirectToAction(nameof(Index));
            }

            await _poiManagementRepository.HardDeletePoiGraphAsync(id);

            TempData["Success"] = "Xóa địa điểm thành công!";
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
        if (string.Equals(HttpContext.Session.GetString("UserRole"), "poi_owner", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Chủ quán không có quyền đổi trạng thái quán.";
            return RedirectToAction(nameof(Index));
        }

        var poi = await _poiManagementRepository.GetPoiByIdAsync(id);
        if (poi != null)
        {
            poi.IsActive = !poi.IsActive;
            await _unitOfWork.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file" });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(new { error = "Invalid file type" });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "File too large (max 5MB)" });

        try
        {
            // In production Web and API run as separate services; store images in Web static folder.
            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

            var imageRoot = Path.Combine(webRoot, "images", "poi");
            Directory.CreateDirectory(imageRoot);

            var safeName = Path.GetFileNameWithoutExtension(file.FileName)
                .ToLowerInvariant()
                .Replace(" ", "-");
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = $"poi-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var randomSuffix = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"{safeName}-{timestamp}-{randomSuffix}{ext}";
            var destPath = Path.Combine(imageRoot, fileName);

            await using (var stream = new FileStream(destPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var relativeUrl = $"/images/poi/{fileName}";
            var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";
            return Ok(new { url = absoluteUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POI image upload failed for file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Upload failed" });
        }
    }

    private async Task EnsureDefaultTranslationsAsync(
        int poiId,
        string name,
        string description,
        string address,
        bool updateVietnamese)
    {
        var translatedValues = await BuildTranslatedValuesAsync(description, address);

        var translations = await _poiManagementRepository.GetTranslationsAsync(poiId);

        var byLang = translations
            .ToDictionary(t => t.LanguageCode, StringComparer.OrdinalIgnoreCase);

        foreach (var lang in LanguageConstants.SupportedLanguages)
        {
            var translated = translatedValues.TryGetValue(lang, out var value)
                ? value
                : (Description: description, Address: address);

            if (byLang.TryGetValue(lang, out var existingTranslation))
            {
                if (updateVietnamese && string.Equals(lang, LanguageConstants.Vietnamese, StringComparison.OrdinalIgnoreCase))
                {
                    existingTranslation.Name = name;
                    existingTranslation.Description = description;
                    existingTranslation.Address = address;
                }

                continue;
            }

            await _poiManagementRepository.AddTranslationAsync(new PointOfInterestTranslation
            {
                PointOfInterestId = poiId,
                LanguageCode = lang,
                Name = name,
                Description = translated.Description,
                Address = translated.Address
            });
        }
    }

    private async Task<Dictionary<string, (string Description, string Address)>> BuildTranslatedValuesAsync(
        string vietnameseDescription,
        string vietnameseAddress)
    {
        var results = new Dictionary<string, (string Description, string Address)>(StringComparer.OrdinalIgnoreCase)
        {
            [LanguageConstants.Vietnamese] = (vietnameseDescription, vietnameseAddress)
        };

        var targetLanguages = LanguageConstants.SupportedLanguages
            .Where(lang => !string.Equals(lang, LanguageConstants.Vietnamese, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var tasks = targetLanguages
            .Select(lang => BuildLanguageTranslationAsync(lang, vietnameseDescription, vietnameseAddress));

        var translated = await Task.WhenAll(tasks);
        foreach (var item in translated)
        {
            results[item.LanguageCode] = (item.Description, item.Address);
        }

        return results;
    }

    private async Task<(string LanguageCode, string Description, string Address)> BuildLanguageTranslationAsync(
        string languageCode,
        string vietnameseDescription,
        string vietnameseAddress)
    {
        var translatedDescription = await _textTranslationService.TranslateAsync(
            vietnameseDescription,
            LanguageConstants.Vietnamese,
            languageCode);

        var translatedAddress = await _textTranslationService.TranslateAsync(
            vietnameseAddress,
            LanguageConstants.Vietnamese,
            languageCode);

        return (languageCode, translatedDescription, translatedAddress);
    }
}
