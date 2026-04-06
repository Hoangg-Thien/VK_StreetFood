namespace VK.Mobile.Services;

public interface IOfflineAudioDownloader
{
    Task<string?> DownloadAudioFileAsync(string audioFileUrl, int poiId, string languageCode, CancellationToken ct);
}
