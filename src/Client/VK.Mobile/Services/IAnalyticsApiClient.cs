using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IAnalyticsApiClient
{
    Task<bool> TrackEventAsync(int? touristId, int poiId, string eventType, string? languageCode = null, int? durationSeconds = null);
    Task<TouristStatsModel?> GetMyStatsAsync(int touristId);
    Task<List<TopPOIModel>> GetTopPOIsAsync(int count = 10);
}
