namespace VK.Mobile.Services;

public class LocalizationApiClient : ILocalizationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalizationApiClient> _logger;

    public LocalizationApiClient(HttpClient httpClient, ILogger<LocalizationApiClient> logger)
    {
        _httpClient = httpClient;
        ApiClientJson.EnsureBaseAddress(_httpClient);
        _logger = logger;
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
            _logger.LogDebug(ex, "PrepareHotsetAsync failed (non-critical)");
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
            _logger.LogDebug(ex, "WarmupAsync failed (non-critical)");
        }
    }
}
