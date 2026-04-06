using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Shared.Constants;
using VK.Web.Services;

namespace VK.Web.Controllers;

public class TourController : AdminBaseController
{
    private readonly IRepository<Tour> _tourRepository;
    private readonly IRepository<TourPointOfInterest> _tourPointRepository;
    private readonly IRepository<TourTranslation> _tourTranslationRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TourController> _logger;
    private readonly ITextTranslationService _textTranslationService;

    public TourController(
        IRepository<Tour> tourRepository,
        IRepository<TourPointOfInterest> tourPointRepository,
        IRepository<TourTranslation> tourTranslationRepository,
        IRepository<PointOfInterest> poiRepository,
        IRepository<Category> categoryRepository,
        IUnitOfWork unitOfWork,
        ILogger<TourController> logger,
        ITextTranslationService textTranslationService)
    {
        _tourRepository = tourRepository;
        _tourPointRepository = tourPointRepository;
        _tourTranslationRepository = tourTranslationRepository;
        _poiRepository = poiRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _textTranslationService = textTranslationService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var status = (Request.Query["status"].ToString() ?? string.Empty).Trim().ToLowerInvariant();

            var toursQuery = _tourRepository.Query()
                .Include(t => t.TourPoints.OrderBy(tp => tp.SortOrder))
                .ThenInclude(tp => tp.PointOfInterest)
                .AsQueryable();

            if (status is "active" or "inactive")
            {
                toursQuery = toursQuery.Where(t => t.Status == status);
            }

            var tours = await toursQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Load POIs to build tours from
            var pois = await _poiRepository.Query()
                .Include(p => p.Category)
                .Include(p => p.AudioContents)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var categories = await _categoryRepository.Query().ToListAsync();

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

            await _tourRepository.AddAsync(tour);
            await _unitOfWork.SaveChangesAsync();
            await EnsureDefaultTranslationsAsync(
                tour.Id,
                tour.Name,
                tour.Description,
                updateVietnamese: true);
            await SyncTourPointsAsync(tour.Id, input.POIIds);

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

            var tour = await _tourRepository.Query()
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

            await EnsureDefaultTranslationsAsync(
                tour.Id,
                tour.Name,
                tour.Description,
                updateVietnamese: true);

            await SyncTourPointsAsync(tour.Id, input.POIIds);

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
            await _tourPointRepository.Query()
                .IgnoreQueryFilters()
                .Where(tp => tp.TourId == id)
                .ExecuteDeleteAsync();

            await _tourTranslationRepository.Query()
                .IgnoreQueryFilters()
                .Where(t => t.TourId == id)
                .ExecuteDeleteAsync();

            var deletedTourCount = await _tourRepository.Query()
                .IgnoreQueryFilters()
                .Where(t => t.Id == id)
                .ExecuteDeleteAsync();

            if (deletedTourCount == 0)
            {
                TempData["Error"] = "Không tìm thấy tour.";
                return RedirectToAction(nameof(Index));
            }

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

        var existing = await _tourPointRepository.Query()
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

            await _tourPointRepository.AddAsync(new TourPointOfInterest
            {
                TourId = tourId,
                PointOfInterestId = poiId,
                SortOrder = sortOrder
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static string NormalizeStatus(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "active" ? "active" : "inactive";
    }

    private async Task EnsureDefaultTranslationsAsync(
        int tourId,
        string name,
        string description,
        bool updateVietnamese)
    {
        var translatedValues = await BuildTranslatedValuesAsync(name, description);

        var translations = await _tourTranslationRepository.Query()
            .Where(t => t.TourId == tourId)
            .ToListAsync();

        var byLang = translations
            .ToDictionary(t => t.LanguageCode, StringComparer.OrdinalIgnoreCase);

        foreach (var lang in LanguageConstants.SupportedLanguages)
        {
            var translated = translatedValues.TryGetValue(lang, out var value)
                ? value
                : (Name: name, Description: description);

            if (byLang.TryGetValue(lang, out var existing))
            {
                if (updateVietnamese && string.Equals(lang, LanguageConstants.Vietnamese, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Name = name;
                    existing.Description = description;
                    continue;
                }

                if (!string.Equals(lang, LanguageConstants.Vietnamese, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Name = translated.Name;
                    existing.Description = translated.Description;
                }

                continue;
            }

            await _tourTranslationRepository.AddAsync(new TourTranslation
            {
                TourId = tourId,
                LanguageCode = lang,
                Name = translated.Name,
                Description = translated.Description
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Dictionary<string, (string Name, string Description)>> BuildTranslatedValuesAsync(
        string vietnameseName,
        string vietnameseDescription)
    {
        var results = new Dictionary<string, (string Name, string Description)>(StringComparer.OrdinalIgnoreCase)
        {
            [LanguageConstants.Vietnamese] = (vietnameseName, vietnameseDescription)
        };

        var tasks = new[]
        {
            BuildLanguageTranslationAsync(LanguageConstants.English, vietnameseName, vietnameseDescription),
            BuildLanguageTranslationAsync(LanguageConstants.Korean, vietnameseName, vietnameseDescription)
        };

        var translated = await Task.WhenAll(tasks);
        foreach (var item in translated)
        {
            results[item.LanguageCode] = (item.Name, item.Description);
        }

        return results;
    }

    private async Task<(string LanguageCode, string Name, string Description)> BuildLanguageTranslationAsync(
        string languageCode,
        string vietnameseName,
        string vietnameseDescription)
    {
        var translatedName = await _textTranslationService.TranslateAsync(
            vietnameseName,
            LanguageConstants.Vietnamese,
            languageCode);

        var translatedDescription = await _textTranslationService.TranslateAsync(
            vietnameseDescription,
            LanguageConstants.Vietnamese,
            languageCode);

        return (languageCode, translatedName, translatedDescription);
    }

    public sealed class TourUpsertInput
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Emoji { get; set; }
        public int? EstimatedDurationMinutes { get; set; }
        public string? Status { get; set; }
        [JsonPropertyName("poiIds")]
        public List<int>? POIIds { get; set; }
    }
}
