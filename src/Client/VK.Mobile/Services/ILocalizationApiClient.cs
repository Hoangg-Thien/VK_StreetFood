namespace VK.Mobile.Services;

public interface ILocalizationApiClient
{
    Task PrepareHotsetAsync(IEnumerable<int> poiIds, string languageCode = "vi", CancellationToken ct = default);
    Task WarmupAsync(string languageCode = "vi", CancellationToken ct = default);
}
