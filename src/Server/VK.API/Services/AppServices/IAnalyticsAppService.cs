using Microsoft.AspNetCore.Mvc;
using VK.API.Models;

namespace VK.API.Services.AppServices;

public interface IAnalyticsAppService
{
    Task<IActionResult> RecordEventAsync(RecordEventRequest request);
    Task<IActionResult> GetPOISummaryAsync(int poiId, DateTime? from, DateTime? to);
    Task<IActionResult> GetDashboardAsync(DateTime? from, DateTime? to);
    Task<IActionResult> GetTopPOIsAsync(int count = 10);
    Task<IActionResult> GetTopListenedPoisAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 10);
    Task<IActionResult> GetAverageListenPerPoiAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 20);
    Task<IActionResult> GetHeatmapAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId);
    Task<IActionResult> GetAnonymousRoutesAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 50);
}
