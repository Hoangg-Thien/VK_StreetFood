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
    Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi");
    Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi");
    Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi");
    Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi");

    // Visits
    Task<bool> LogVisitAsync(int touristId, int poiId, string triggerMethod, double? latitude = null, double? longitude = null);
    Task<List<VisitLogModel>> GetVisitHistoryAsync(int touristId);

    // Favorites
    Task<bool> AddFavoriteAsync(int touristId, int poiId);
    Task<bool> RemoveFavoriteAsync(int touristId, int poiId);
    Task<List<POIModel>> GetFavoritesAsync(int touristId, string languageCode = "vi");

    // Audio
    Task<AudioContentResult?> GetAudioForPOIAsync(int poiId, string languageCode = "vi");

    /// <summary>
    /// Tier 2: On-demand TTS — server tự generate MP3 nếu chưa có, trả về URL để play.
    /// </summary>
    Task<AudioContentResult?> RequestOnDemandTtsAsync(int poiId, string languageCode = "vi", CancellationToken ct = default);

    // Rating
    Task<bool> SubmitRatingAsync(int touristId, int poiId, int rating, string? comment = null);

    // Analytics
    Task<bool> TrackEventAsync(int? touristId, int poiId, string eventType, string? languageCode = null, int? durationSeconds = null);
    Task<TouristStatsModel?> GetMyStatsAsync(int touristId);
    Task<List<TopPOIModel>> GetTopPOIsAsync(int count = 10);

    // Tours
    Task<List<TourModel>> GetToursAsync();
    Task<TourModel?> GetTourByIdAsync(int tourId);

    // Localization
    /// <summary>Hotset: Pre-warm audio cho top N POI gần nhất khi mở app.</summary>
    Task PrepareHotsetAsync(IEnumerable<int> poiIds, string languageCode = "vi", CancellationToken ct = default);
    /// <summary>Warmup: Generate toàn bộ audio corpus còn thiếu dưới nền.</summary>
    Task WarmupAsync(string languageCode = "vi", CancellationToken ct = default);
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

    public async Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi")
    {
        try
        {
            var url = $"poi?languageCode={Uri.EscapeDataString(languageCode)}";
            if (!string.IsNullOrEmpty(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            var fullUrl = new Uri(_httpClient.BaseAddress!, url);
            _logger.LogInformation("Fetching POIs from: {Url}", fullUrl);
            System.Diagnostics.Debug.WriteLine($"[ApiService] GET {fullUrl}");

            using var response = await _httpClient.GetAsync(url);
            var rawJson = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"[ApiService] POI response status: {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[ApiService] POI raw JSON (first 500): {rawJson.Substring(0, Math.Min(500, rawJson.Length))}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("POI API returned {StatusCode}: {Body}", response.StatusCode, rawJson);
                return new List<POIModel>();
            }

            var pois = JsonSerializer.Deserialize<List<POIModel>>(rawJson, _jsonOptions);
            _logger.LogInformation("Deserialized {Count} POIs", pois?.Count ?? 0);
            System.Diagnostics.Debug.WriteLine($"[ApiService] Deserialized {pois?.Count ?? 0} POIs");

            if (pois != null && pois.Count > 0)
            {
                var first = pois[0];
                System.Diagnostics.Debug.WriteLine($"[ApiService] First POI: Id={first.Id} Name='{first.Name}' Lat={first.Latitude} Lon={first.Longitude} Category='{first.CategoryName}'");
            }

            return pois ?? new List<POIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POIs");
            System.Diagnostics.Debug.WriteLine($"[ApiService] GetAllPOIsAsync EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            return new List<POIModel>();
        }
    }

    public async Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi")
    {
        try
        {
            var url = $"poi/nearby?latitude={latitude}&longitude={longitude}&radiusKm={radiusKm}&languageCode={Uri.EscapeDataString(languageCode)}";
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

    public async Task<AudioContentResult?> GetAudioForPOIAsync(int poiId, string languageCode = "vi")
    {
        try
        {
            var url = $"audio/poi/{poiId}?languageCode={languageCode}";
            System.Diagnostics.Debug.WriteLine($"[ApiService] GET audio: {url}");
            return await _httpClient.GetFromJsonAsync<AudioContentResult>(url, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audio for POI {Id}", poiId);
            return null;
        }
    }

    public async Task<AudioContentResult?> RequestOnDemandTtsAsync(
        int poiId, string languageCode = "vi", CancellationToken ct = default)
    {
        try
        {
            var request = new { poiId, languageCode };
            var response = await _httpClient.PostAsJsonAsync("audio/tts", request, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AudioContentResult>(_jsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ApiService] On-demand TTS failed for POI {Id}", poiId);
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
            var request = new { PoiId = poiId };
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

    public async Task<List<POIModel>> GetFavoritesAsync(int touristId, string languageCode = "vi")
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<POIModel>>(
                $"tourist/{touristId}/favorites?languageCode={Uri.EscapeDataString(languageCode)}",
                _jsonOptions) ?? new List<POIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorites");
            return new List<POIModel>();
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

    public async Task<List<TourModel>> GetToursAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TourModel>>("tour", _jsonOptions) ?? new List<TourModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tours");
            return new List<TourModel>();
        }
    }

    public async Task<TourModel?> GetTourByIdAsync(int tourId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TourModel>($"tour/{tourId}", _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tour detail for {TourId}", tourId);
            return null;
        }
    }

    public async Task PrepareHotsetAsync(IEnumerable<int> poiIds, string languageCode = "vi", CancellationToken ct = default)
    {
        try
        {
            var body = new { poiIds = poiIds.ToList(), languageCode };
            await _httpClient.PostAsJsonAsync("localizations/prepare-hotset", body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ApiService] PrepareHotsetAsync failed (non-critical)");
        }
    }

    public async Task WarmupAsync(string languageCode = "vi", CancellationToken ct = default)
    {
        try
        {
            var body = new { languageCode };
            await _httpClient.PostAsJsonAsync("localizations/warmup", body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ApiService] WarmupAsync failed (non-critical)");
        }
    }
}
