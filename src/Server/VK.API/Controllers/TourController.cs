using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TourController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;

    public TourController(VKStreetFoodDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<TourListItemDto>>> GetTours([FromQuery] string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var tours = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.TourPoints.OrderBy(tp => tp.SortOrder))
            .ThenInclude(tp => tp.PointOfInterest)
            .Where(t => t.Status == "active" || t.Status == "draft" || t.Status == "inactive")
            .OrderByDescending(t => t.Status == "active")
            .ThenBy(t => t.Name)
            .ToListAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var result = tours.Select(t =>
        {
            var orderedPoints = t.TourPoints
                .Where(tp => tp.PointOfInterest != null)
                .OrderBy(tp => tp.SortOrder)
                .ToList();

            var firstPoi = orderedPoints.FirstOrDefault()?.PointOfInterest;

            var dto = new TourListItemDto
            {
                TourId = t.Id,
                Name = t.Name,
                Description = t.Description,
                Emoji = string.IsNullOrWhiteSpace(t.Emoji) ? "🍜" : t.Emoji,
                EstimatedDurationMinutes = t.EstimatedDurationMinutes,
                Status = t.Status,
                StopsCount = orderedPoints.Count,
                FirstPOIId = firstPoi?.Id,
                CoverImageUrl = PrependBase(baseUrl, firstPoi?.ImageUrl)
            };

            ApplyLocalizedFields(dto, t, normalizedLanguageCode);
            return dto;
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{tourId:int}")]
    public async Task<ActionResult<TourDetailDto>> GetTourById(int tourId, [FromQuery] string languageCode = LanguageConstants.Vietnamese)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var tour = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.TourPoints.OrderBy(tp => tp.SortOrder))
            .ThenInclude(tp => tp.PointOfInterest)
            .ThenInclude(p => p.Translations)
            .FirstOrDefaultAsync(t => t.Id == tourId);

        if (tour == null)
            return NotFound(new { message = "Tour không tồn tại" });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var points = tour.TourPoints
            .Where(tp => tp.PointOfInterest != null)
            .OrderBy(tp => tp.SortOrder)
            .Select(tp =>
            {
                var point = new TourPointDto
                {
                    POIId = tp.PointOfInterest.Id,
                    Name = tp.PointOfInterest.Name,
                    Address = tp.PointOfInterest.Address,
                    ImageUrl = PrependBase(baseUrl, tp.PointOfInterest.ImageUrl),
                    Latitude = tp.PointOfInterest.Latitude,
                    Longitude = tp.PointOfInterest.Longitude,
                    SortOrder = tp.SortOrder
                };

                ApplyLocalizedPoiFields(point, tp.PointOfInterest, normalizedLanguageCode);
                return point;
            })
            .ToList();

        var detail = new TourDetailDto
        {
            TourId = tour.Id,
            Name = tour.Name,
            Description = tour.Description,
            Emoji = string.IsNullOrWhiteSpace(tour.Emoji) ? "🍜" : tour.Emoji,
            EstimatedDurationMinutes = tour.EstimatedDurationMinutes,
            Status = tour.Status,
            StopsCount = points.Count,
            FirstPOIId = points.FirstOrDefault()?.POIId,
            CoverImageUrl = points.FirstOrDefault()?.ImageUrl,
            Points = points
        };

        ApplyLocalizedFields(detail, tour, normalizedLanguageCode);

        return Ok(detail);
    }

    private static void ApplyLocalizedFields(TourListItemDto dto, Tour tour, string languageCode)
    {
        var translation = ResolveTourTranslation(tour, languageCode);
        if (translation == null)
            return;

        if (!string.IsNullOrWhiteSpace(translation.Name))
            dto.Name = translation.Name;

        if (!string.IsNullOrWhiteSpace(translation.Description))
            dto.Description = translation.Description;
    }

    private static TourTranslation? ResolveTourTranslation(Tour tour, string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return tour.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == normalized)
            ?? tour.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == LanguageConstants.Vietnamese);
    }

    private static void ApplyLocalizedPoiFields(TourPointDto dto, PointOfInterest poi, string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        var translation = poi.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == normalized)
            ?? poi.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == LanguageConstants.Vietnamese);

        if (translation == null)
            return;

        if (!string.IsNullOrWhiteSpace(translation.Name))
            dto.Name = translation.Name;

        if (!string.IsNullOrWhiteSpace(translation.Address))
            dto.Address = translation.Address;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return LanguageConstants.Vietnamese;

        var code = languageCode.Trim().ToLowerInvariant();
        var separatorIndex = code.IndexOfAny(new[] { '-', '_' });
        return separatorIndex > 0 ? code[..separatorIndex] : code;
    }

    private static string? PrependBase(string baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
        return baseUrl + path;
    }
}