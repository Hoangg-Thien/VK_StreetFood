using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VK.API.Common;
using VK.API.Models;
using VK.API.Services.AppServices;
using VK.Shared.DTOs;

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
    [ProducesResponseType(typeof(RecordEventResultDto), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RecordEvent([FromBody] RecordEventRequest request)
        => (await _analyticsAppService.RecordEventAsync(request)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("poi/{poiId}/summary")]
    [ProducesResponseType(typeof(POISummaryDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetPOISummary(int poiId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => (await _analyticsAppService.GetPOISummaryAsync(poiId, from, to)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => (await _analyticsAppService.GetDashboardAsync(from, to)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("top-pois")]
    [ProducesResponseType(typeof(IReadOnlyList<TopPoiDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetTopPOIs([FromQuery] int count = 10)
        => (await _analyticsAppService.GetTopPOIsAsync(count)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("top-listened-pois")]
    [ProducesResponseType(typeof(IReadOnlyList<TopListenedPoiDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetTopListenedPois(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 10)
        => (await _analyticsAppService.GetTopListenedPoisAsync(from, to, languageCode, poiId, take)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("avg-listen-per-poi")]
    [ProducesResponseType(typeof(IReadOnlyList<AvgListenPoiDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetAverageListenPerPoi(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 20)
        => (await _analyticsAppService.GetAverageListenPerPoiAsync(from, to, languageCode, poiId, take)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(IReadOnlyList<HeatmapPointDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId)
        => (await _analyticsAppService.GetHeatmapAsync(from, to, languageCode, poiId)).ToActionResult();

    [Authorize(Roles = "Admin")]
    [HttpGet("anonymous-routes")]
    [ProducesResponseType(typeof(IReadOnlyList<AnonymousRouteDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetAnonymousRoutes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode,
        [FromQuery] int? poiId,
        [FromQuery] int take = 50)
        => (await _analyticsAppService.GetAnonymousRoutesAsync(from, to, languageCode, poiId, take)).ToActionResult();
}
