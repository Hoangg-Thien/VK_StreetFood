using Microsoft.AspNetCore.Mvc;
using VK.API.Services.AppServices;
using VK.Shared.Constants;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class POIController : ControllerBase
{
    private readonly IPOIAppService _poiAppService;

    public POIController(IPOIAppService poiAppService)
    {
        _poiAppService = poiAppService;
    }

    /// <summary>
    /// Get all POIs (Points of Interest)
    /// </summary>
    [HttpGet]
    public Task<IActionResult> GetAllPOIs(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => _poiAppService.GetAllPOIsAsync(categoryId, search, languageCode);

    /// <summary>
    /// Get POIs near a specific location (GPS-based)
    /// </summary>
    [HttpGet("nearby")]
    public Task<IActionResult> GetNearbyPOIs(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 1.0,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => _poiAppService.GetNearbyPOIsAsync(latitude, longitude, radiusKm, languageCode);

    /// <summary>
    /// Get POI details by ID
    /// </summary>
    [HttpGet("{id}")]
    public Task<IActionResult> GetPOIById(int id, [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => _poiAppService.GetPOIByIdAsync(id, languageCode);

    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet("categories")]
    public Task<IActionResult> GetCategories()
        => _poiAppService.GetCategoriesAsync();
}
