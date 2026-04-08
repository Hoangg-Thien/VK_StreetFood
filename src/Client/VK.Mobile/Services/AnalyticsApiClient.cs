using VK.Mobile.Models;

namespace VK.Mobile.Services;

public class AnalyticsApiClient : IAnalyticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnalyticsApiClient> _logger;

    public AnalyticsApiClient(HttpClient httpClient, ILogger<AnalyticsApiClient> logger)
    {
        _httpClient = httpClient;
        ApiClientJson.EnsureBaseAddress(_httpClient);
        _logger = logger;
    }

    public async Task<bool> TrackEventAsync(int? touristId, int poiId, string eventType, string? languageCode = null, int? durationSeconds = null)
    {
        try
        {
            var request = new { touristId, poiId, eventType, languageCode, durationSeconds };
            var response = await _httpClient.PostAsJsonAsync("analytics/event", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking event");
            return false;
        }
    }

    public async Task<TouristStatsModel?> GetMyStatsAsync(int touristId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TouristStatsModel>($"tourist/{touristId}/stats", ApiClientJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tourist stats");
            return null;
        }
    }

    public async Task<List<TopPOIModel>> GetTopPOIsAsync(int count = 10)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TopPOIModel>>($"analytics/top-pois?count={count}", ApiClientJson.Options) ?? new List<TopPOIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top POIs");
            return new List<TopPOIModel>();
        }
    }
}
