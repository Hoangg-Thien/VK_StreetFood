using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VK.API.Models;
using VK.API.Services.AppServices;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsAppService _analyticsAppService;

    public AnalyticsController(IAnalyticsAppService analyticsAppService)
    {
        _analyticsAppService = analyticsAppService;
    }

    /// <summary>Record an analytics event (tourist-facing, no auth required).</summary>
    [AllowAnonymous]
    [HttpPost("event")]
    public Task<IActionResult> RecordEvent([FromBody] RecordEventRequest request)
        => _analyticsAppService.RecordEventAsync(request);

    [Authorize(Roles = "Admin")]
    [HttpGet("poi/{poiId}/summary")]
    public Task<IActionResult> GetPOISummary(int poiId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => _analyticsAppService.GetPOISummaryAsync(poiId, from, to);

    [Authorize(Roles = "Admin")]
    [HttpGet("dashboard")]
    public Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => _analyticsAppService.GetDashboardAsync(from, to);

    [Authorize(Roles = "Admin")]
    [HttpGet("top-pois")]
    public Task<IActionResult> GetTopPOIs([FromQuery] int count = 10)
        => _analyticsAppService.GetTopPOIsAsync(count);

    [Authorize(Roles = "Admin")]
    [HttpGet("top-listened-pois")]
    public Task<IActionResult> GetTopListenedPois(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 10)
        => _analyticsAppService.GetTopListenedPoisAsync(from, to, languageCode, poiId, take);

    [Authorize(Roles = "Admin")]
    [HttpGet("avg-listen-per-poi")]
    public Task<IActionResult> GetAverageListenPerPoi(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 20)
        => _analyticsAppService.GetAverageListenPerPoiAsync(from, to, languageCode, poiId, take);

    [Authorize(Roles = "Admin")]
    [HttpGet("heatmap")]
    public Task<IActionResult> GetHeatmap(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId)
        => _analyticsAppService.GetHeatmapAsync(from, to, languageCode, poiId);

    [Authorize(Roles = "Admin")]
    [HttpGet("anonymous-routes")]
    public Task<IActionResult> GetAnonymousRoutes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 50)
        => _analyticsAppService.GetAnonymousRoutesAsync(from, to, languageCode, poiId, take);
}
