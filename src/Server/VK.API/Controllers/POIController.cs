using Microsoft.AspNetCore.Mvc;
using VK.API.Services.AppServices;
using VK.Contracts.Responses;
using VK.Shared.Constants;
using VK.Shared.DTOs;

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
    [ProducesResponseType(typeof(IReadOnlyList<POIListItemDto>), 200)]
    public async Task<IActionResult> GetAllPOIs(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => Ok(await _poiAppService.GetAllPOIsAsync(categoryId, search, languageCode));

    /// <summary>
    /// Get paged POIs (Points of Interest)
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResponse<POIListItemDto>), 200)]
    public async Task<IActionResult> GetPagedPOIs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => Ok(await _poiAppService.GetPagedPOIsAsync(pageNumber, pageSize, categoryId, search, languageCode));

    /// <summary>
    /// Get POIs near a specific location (GPS-based)
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(IReadOnlyList<POIListItemDto>), 200)]
    public async Task<IActionResult> GetNearbyPOIs(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 1.0,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => Ok(await _poiAppService.GetNearbyPOIsAsync(latitude, longitude, radiusKm, languageCode));

    /// <summary>
    /// Get POI details by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(POIDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPOIById(int id, [FromQuery] string languageCode = LanguageConstants.Vietnamese)
    {
        var poi = await _poiAppService.GetPOIByIdAsync(id, languageCode);
        return poi is null ? NotFound(new { message = "POI không tồn tại" }) : Ok(poi);
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), 200)]
    public async Task<IActionResult> GetCategories() => Ok(await _poiAppService.GetCategoriesAsync());
}
