using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
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
    public async Task<ActionResult<List<TourListItemDto>>> GetTours()
    {
        var tours = await _context.Tours
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

            return new TourListItemDto
            {
                TourId = t.Id,
                Name = t.Name,
                Description = t.Description,
                Emoji = string.IsNullOrWhiteSpace(t.Emoji) ? "🍜" : t.Emoji,
                EstimatedDurationMinutes = t.EstimatedDurationMinutes,
                Status = t.Status,
                StopsCount = orderedPoints.Count,
                FirstPoiId = firstPoi?.Id,
                CoverImageUrl = PrependBase(baseUrl, firstPoi?.ImageUrl)
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{tourId:int}")]
    public async Task<ActionResult<TourDetailDto>> GetTourById(int tourId)
    {
        var tour = await _context.Tours
            .Include(t => t.TourPoints.OrderBy(tp => tp.SortOrder))
            .ThenInclude(tp => tp.PointOfInterest)
            .FirstOrDefaultAsync(t => t.Id == tourId);

        if (tour == null)
            return NotFound(new { message = "Tour không tồn tại" });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var points = tour.TourPoints
            .Where(tp => tp.PointOfInterest != null)
            .OrderBy(tp => tp.SortOrder)
            .Select(tp => new TourPointDto
            {
                PoiId = tp.PointOfInterest.Id,
                Name = tp.PointOfInterest.Name,
                Address = tp.PointOfInterest.Address,
                ImageUrl = PrependBase(baseUrl, tp.PointOfInterest.ImageUrl),
                Latitude = tp.PointOfInterest.Latitude,
                Longitude = tp.PointOfInterest.Longitude,
                SortOrder = tp.SortOrder
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
            FirstPoiId = points.FirstOrDefault()?.PoiId,
            CoverImageUrl = points.FirstOrDefault()?.ImageUrl,
            Points = points
        };

        return Ok(detail);
    }

    private static string? PrependBase(string baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
        return baseUrl + path;
    }
}