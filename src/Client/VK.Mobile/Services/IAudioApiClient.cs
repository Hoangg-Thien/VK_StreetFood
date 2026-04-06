using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IAudioApiClient
{
    Task<AudioContentResult?> GetAudioForPOIAsync(int poiId, string languageCode = "vi");
    Task<AudioContentResult?> RequestOnDemandTtsAsync(int poiId, string languageCode = "vi", CancellationToken ct = default);
}
