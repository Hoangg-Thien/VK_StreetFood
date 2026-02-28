using System.Net.Http.Json;
using System.Text.Json;
using VK.Mobile.Models;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.Services;

public interface IApiService
{
    // Tourist
    Task<TouristModel?> RegisterTouristAsync(string deviceId, string preferredLanguage, double? latitude = null, double? longitude = null);
    Task<bool> UpdateLocationAsync(int touristId, double latitude, double longitude);

    // POI
    Task<List<POIModel>> GetAllPOIsAsync(string? search = null);
    Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0);
    Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi");
    Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi");

    // Visits
    Task<bool> LogVisitAsync(int touristId, int poiId, string triggerMethod, double? latitude = null, double? longitude = null);
    Task<List<VisitLogModel>> GetVisitHistoryAsync(int touristId);

    // Favorites
    Task<bool> AddFavoriteAsync(int touristId, int poiId);
    Task<bool> RemoveFavoriteAsync(int touristId, int poiId);
    Task<List<FavoriteModel>> GetFavoritesAsync(int touristId);

    // Rating
    Task<bool> SubmitRatingAsync(int touristId, int poiId, int rating, string? comment = null);

    // Analytics
    Task<bool> TrackEventAsync(int? touristId, int poiId, string eventType, string? languageCode = null, int? durationSeconds = null);
    Task<TouristStatsModel?> GetMyStatsAsync(int touristId);
    Task<List<TopPOIModel>> GetTopPOIsAsync(int count = 10);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    /// <summary>
    /// Case-insensitive + camelCase: đảm bảo JSON từ API (camelCase) 
    /// deserialize đúng vào mobile models.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(AppSettings.ApiBaseUrl);
        _logger = logger;
    }

    public async Task<TouristModel?> RegisterTouristAsync(string deviceId, string preferredLanguage, double? latitude = null, double? longitude = null)
    {
        try
        {
            var request = new { deviceId, preferredLanguage, latitude, longitude };
            var response = await _httpClient.PostAsJsonAsync("tourist/register", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TouristModel>(_jsonOptions);
            _logger.LogInformation("Tourist registered: {DeviceId}", deviceId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering tourist");
            return null;
        }
    }

    public async Task<bool> UpdateLocationAsync(int touristId, double latitude, double longitude)
    {
        try
        {
            var request = new { latitude, longitude };
            var response = await _httpClient.PutAsJsonAsync($"tourist/{touristId}/location", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location");
            return false;
        }
    }

    public async Task<List<POIModel>> GetAllPOIsAsync(string? search = null)
    {
        try
        {
            var url = "poi";
            if (!string.IsNullOrEmpty(search))
                url += $"?search={Uri.EscapeDataString(search)}";

            var pois = await _httpClient.GetFromJsonAsync<List<POIModel>>(url, _jsonOptions);
            return pois ?? new List<POIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POIs");
            return new List<POIModel>();
        }
    }

    public async Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0)
    {
        try
        {
            var url = $"poi/nearby?latitude={latitude}&longitude={longitude}&radiusKm={radiusKm}";
            var pois = await _httpClient.GetFromJsonAsync<List<POIModel>>(url, _jsonOptions);
            return pois ?? new List<POIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nearby POIs");
            return new List<POIModel>();
        }
    }

    public async Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi")
    {
        try
        {
            var url = $"poi/{poiId}?languageCode={languageCode}";
            return await _httpClient.GetFromJsonAsync<POIDetailModel>(url, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POI detail");
            return null;
        }
    }

    public async Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi")
    {
        try
        {
            var url = $"qrcode/scan/{qrCode}?languageCode={languageCode}";
            return await _httpClient.GetFromJsonAsync<POIDetailModel>(url, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning QR code");
            return null;
        }
    }

    public async Task<bool> LogVisitAsync(int touristId, int poiId, string triggerMethod, double? latitude = null, double? longitude = null)
    {
        try
        {
            var request = new { pointOfInterestId = poiId, triggerMethod, latitude, longitude };
            var response = await _httpClient.PostAsJsonAsync($"tourist/{touristId}/visits", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging visit");
            return false;
        }
    }

    public async Task<List<VisitLogModel>> GetVisitHistoryAsync(int touristId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<VisitLogModel>>($"tourist/{touristId}/visits", _jsonOptions) ?? new List<VisitLogModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting visit history");
            return new List<VisitLogModel>();
        }
    }

    public async Task<bool> AddFavoriteAsync(int touristId, int poiId)
    {
        try
        {
            var request = new { pointOfInterestId = poiId };
            var response = await _httpClient.PostAsJsonAsync($"tourist/{touristId}/favorites", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite");
            return false;
        }
    }

    public async Task<bool> RemoveFavoriteAsync(int touristId, int poiId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"tourist/{touristId}/favorites/{poiId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite");
            return false;
        }
    }

    public async Task<List<FavoriteModel>> GetFavoritesAsync(int touristId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<FavoriteModel>>($"tourist/{touristId}/favorites", _jsonOptions) ?? new List<FavoriteModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorites");
            return new List<FavoriteModel>();
        }
    }

    public async Task<bool> SubmitRatingAsync(int touristId, int poiId, int rating, string? comment = null)
    {
        try
        {
            var request = new { pointOfInterestId = poiId, ratingValue = rating, comment };
            var response = await _httpClient.PostAsJsonAsync($"tourist/{touristId}/ratings", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting rating");
            return false;
        }
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
            return await _httpClient.GetFromJsonAsync<TouristStatsModel>($"tourist/{touristId}/stats", _jsonOptions);
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
            return await _httpClient.GetFromJsonAsync<List<TopPOIModel>>($"analytics/top-pois?count={count}", _jsonOptions) ?? new List<TopPOIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top POIs");
            return new List<TopPOIModel>();
        }
    }
}
