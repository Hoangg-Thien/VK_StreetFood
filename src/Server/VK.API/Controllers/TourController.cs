using Microsoft.AspNetCore.Mvc;
using VK.API.Services.AppServices;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TourController : ControllerBase
{
    private readonly ITourAppService _tourAppService;

    public TourController(ITourAppService tourAppService)
    {   
        _tourAppService = tourAppService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TourListItemDto>), 200)]
    public async Task<IActionResult> GetTours([FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => Ok(await _tourAppService.GetToursAsync(languageCode));

    [HttpGet("{tourId:int}")]
    [ProducesResponseType(typeof(TourDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTourById(int tourId, [FromQuery] string languageCode = LanguageConstants.Vietnamese)
    {
        var tour = await _tourAppService.GetTourByIdAsync(tourId, languageCode);
        return tour is null
        ? NotFound(new { message = "Tour không tồn tại" })
        : Ok(tour);
    }
}