using VK.Mobile.Models;

namespace VK.Mobile.Services;

public class TouristApiClient : ITouristApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TouristApiClient> _logger;

    public TouristApiClient(HttpClient httpClient, ILogger<TouristApiClient> logger)
    {
        _httpClient = httpClient;
        ApiClientJson.EnsureBaseAddress(_httpClient);
        _logger = logger;
    }

    public async Task<TouristModel?> RegisterTouristAsync(string deviceId, string preferredLanguage, double? latitude = null, double? longitude = null)
    {
        try
        {
            var request = new { deviceId, preferredLanguage, latitude, longitude };
            var response = await _httpClient.PostAsJsonAsync("tourist/register", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TouristModel>(ApiClientJson.Options);
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
            return await _httpClient.GetFromJsonAsync<List<VisitLogModel>>($"tourist/{touristId}/visits", ApiClientJson.Options) ?? new List<VisitLogModel>();
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
            var request = new { POIId = poiId };
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
                ApiClientJson.Options) ?? new List<POIModel>();
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

    public async Task<QrPaymentConfigModel?> GetQrPaymentConfigAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<QrPaymentConfigModel>("payment/qr-config", ApiClientJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QR payment config");
            return null;
        }
    }
}
