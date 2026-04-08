using VK.Mobile.Models;

namespace VK.Mobile.Services;

public class AudioApiClient : IAudioApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AudioApiClient> _logger;

    public AudioApiClient(HttpClient httpClient, ILogger<AudioApiClient> logger)
    {
        _httpClient = httpClient;
        ApiClientJson.EnsureBaseAddress(_httpClient);
        _logger = logger;
    }

    public async Task<AudioContentResult?> GetAudioForPOIAsync(int poiId, string languageCode = "vi")
    {
        try
        {
            var url = $"audio/poi/{poiId}?languageCode={languageCode}";
            return await _httpClient.GetFromJsonAsync<AudioContentResult>(url, ApiClientJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audio for POI {Id}", poiId);
            return null;
        }
    }

    public async Task<AudioContentResult?> RequestOnDemandTtsAsync(int poiId, string languageCode = "vi", CancellationToken ct = default)
    {
        try
        {
            var request = new { poiId, languageCode };
            var response = await _httpClient.PostAsJsonAsync("audio/tts", request, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AudioContentResult>(ApiClientJson.Options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "On-demand TTS failed for POI {Id}", poiId);
            return null;
        }
    }
}
