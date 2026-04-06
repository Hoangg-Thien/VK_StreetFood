using Microsoft.AspNetCore.Mvc;
using VK.API.Services.AppServices;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TouristController : ControllerBase
{
    private readonly ITouristAppService _touristAppService;

    public TouristController(ITouristAppService touristAppService)
    {
        _touristAppService = touristAppService;
    }

    [HttpPost("register")]
    public Task<IActionResult> RegisterTourist([FromBody] RegisterTouristRequest request)
        => _touristAppService.RegisterTouristAsync(request);

    [HttpPut("{touristId}/location")]
    public Task<IActionResult> UpdateLocation(int touristId, [FromBody] UpdateLocationRequest request)
        => _touristAppService.UpdateLocationAsync(touristId, request);

    [HttpPost("{touristId}/visits")]
    public Task<IActionResult> LogVisit(int touristId, [FromBody] LogVisitRequest request)
        => _touristAppService.LogVisitAsync(touristId, request);

    [HttpGet("{touristId}/visits")]
    public Task<IActionResult> GetVisitHistory(int touristId)
        => _touristAppService.GetVisitHistoryAsync(touristId);

    [HttpPost("{touristId}/favorites")]
    public Task<IActionResult> AddFavorite(int touristId, [FromBody] AddFavoriteRequest request)
        => _touristAppService.AddFavoriteAsync(touristId, request);

    [HttpDelete("{touristId}/favorites/{poiId}")]
    public Task<IActionResult> RemoveFavorite(int touristId, int poiId)
        => _touristAppService.RemoveFavoriteAsync(touristId, poiId);

    [HttpGet("{touristId}/favorites")]
    public Task<IActionResult> GetFavorites(
        int touristId,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => _touristAppService.GetFavoritesAsync(touristId, languageCode);

    [HttpPost("{touristId}/ratings")]
    public Task<IActionResult> SubmitRating(int touristId, [FromBody] SubmitRatingRequest request)
        => _touristAppService.SubmitRatingAsync(touristId, request);

    [HttpGet("{touristId}/stats")]
    public Task<IActionResult> GetStats(int touristId)
        => _touristAppService.GetStatsAsync(touristId);
}
