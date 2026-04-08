using Microsoft.AspNetCore.Mvc;
using VK.API.Services.AppServices;
using VK.Shared.Constants;

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
    public Task<IActionResult> GetTours([FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => _tourAppService.GetToursAsync(languageCode);

    [HttpGet("{tourId:int}")]
    public Task<IActionResult> GetTourById(int tourId, [FromQuery] string languageCode = LanguageConstants.Vietnamese)
        => _tourAppService.GetTourByIdAsync(tourId, languageCode);
}