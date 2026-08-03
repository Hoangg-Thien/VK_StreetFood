using VK.API.Common;
using VK.API.Models;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public interface IAnalyticsAppService
{
    Task<ServiceResult<RecordEventResultDto>> RecordEventAsync(RecordEventRequest request);
    Task<ServiceResult<POISummaryDto>> GetPOISummaryAsync(int poiId, DateTime? from, DateTime? to);
    Task<ServiceResult<DashboardDto>> GetDashboardAsync(DateTime? from, DateTime? to);
    Task<ServiceResult<IReadOnlyList<TopPoiDto>>> GetTopPOIsAsync(int count = 10);
    Task<ServiceResult<IReadOnlyList<TopListenedPoiDto>>> GetTopListenedPoisAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 10);
    Task<ServiceResult<IReadOnlyList<AvgListenPoiDto>>> GetAverageListenPerPoiAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 20);
    Task<ServiceResult<IReadOnlyList<HeatmapPointDto>>> GetHeatmapAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId);
    Task<ServiceResult<IReadOnlyList<AnonymousRouteDto>>> GetAnonymousRoutesAsync(DateTime? from, DateTime? to, string? languageCode, int? poiId, int take = 50);
}
