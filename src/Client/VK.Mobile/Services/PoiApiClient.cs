using System.Text.Json;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public class PoiApiClient : IPoiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PoiApiClient> _logger;

    public PoiApiClient(HttpClient httpClient, ILogger<PoiApiClient> logger)
    {
        _httpClient = httpClient;
        ApiClientJson.EnsureBaseAddress(_httpClient);
        _logger = logger;
    }

    public async Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi")
    {
        try
        {
            var url = $"poi?languageCode={Uri.EscapeDataString(languageCode)}";
            if (!string.IsNullOrEmpty(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            using var response = await _httpClient.GetAsync(url);
            var rawJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("POI API returned {StatusCode}: {Body}", response.StatusCode, rawJson);
                return new List<POIModel>();
            }

            var pois = JsonSerializer.Deserialize<List<POIModel>>(rawJson, ApiClientJson.Options);
            return pois ?? new List<POIModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POIs");
            return new List<POIModel>();
        }
    }

    public async Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi")
    {
        try
        {
            var url = $"poi/nearby?latitude={latitude}&longitude={longitude}&radiusKm={radiusKm}&languageCode={Uri.EscapeDataString(languageCode)}";
            var pois = await _httpClient.GetFromJsonAsync<List<POIModel>>(url, ApiClientJson.Options);
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
            return await _httpClient.GetFromJsonAsync<POIDetailModel>(url, ApiClientJson.Options);
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
            return await _httpClient.GetFromJsonAsync<POIDetailModel>(url, ApiClientJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning QR code");
            return null;
        }
    }
}
