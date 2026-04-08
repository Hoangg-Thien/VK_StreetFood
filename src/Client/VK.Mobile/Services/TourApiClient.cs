using VK.Mobile.Models;

namespace VK.Mobile.Services;

public class TourApiClient : ITourApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TourApiClient> _logger;

    public TourApiClient(HttpClient httpClient, ILogger<TourApiClient> logger)
    {
        _httpClient = httpClient;
        ApiClientJson.EnsureBaseAddress(_httpClient);
        _logger = logger;
    }

    public async Task<List<TourModel>> GetToursAsync(string languageCode = "vi")
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TourModel>>(
                $"tour?languageCode={Uri.EscapeDataString(languageCode)}",
                ApiClientJson.Options) ?? new List<TourModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tours");
            return new List<TourModel>();
        }
    }

    public async Task<TourModel?> GetTourByIdAsync(int tourId, string languageCode = "vi")
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TourModel>(
                $"tour/{tourId}?languageCode={Uri.EscapeDataString(languageCode)}",
                ApiClientJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tour detail for {TourId}", tourId);
            return null;
        }
    }
}
