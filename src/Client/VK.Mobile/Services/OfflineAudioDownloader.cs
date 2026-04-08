using System.Globalization;

namespace VK.Mobile.Services;

public class OfflineAudioDownloader : IOfflineAudioDownloader
{
    private readonly HttpClient _downloadClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ILogger<OfflineAudioDownloader> _logger;

    public OfflineAudioDownloader(ILogger<OfflineAudioDownloader> logger)
    {
        _logger = logger;
    }

    public async Task<string?> DownloadAudioFileAsync(string audioFileUrl, int poiId, string languageCode, CancellationToken ct)
    {
        try
        {
            var absoluteUrl = ToAbsoluteUrl(audioFileUrl);
            var uri = new Uri(absoluteUrl);

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".mp3";

            var folder = Path.Combine(FileSystem.AppDataDirectory, "offline_audio");
            Directory.CreateDirectory(folder);

            var fileName = $"poi_{poiId}_{NormalizeLanguage(languageCode)}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
                return fullPath;

            using var response = await _downloadClient.GetAsync(absoluteUrl, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            await File.WriteAllBytesAsync(fullPath, bytes, ct);
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Download audio file failed for POI {PoiId}", poiId);
            return null;
        }
    }

    private static string ToAbsoluteUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUrl = AppSettings.AudioBaseUrl.TrimEnd('/');
        if (url.StartsWith('/'))
            return baseUrl + url;

        return $"{baseUrl}/{url}";
    }

    private static string NormalizeLanguage(string languageCode)
        => string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode.Trim().ToLowerInvariant();
}
