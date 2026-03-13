using Microsoft.Extensions.Logging;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IOfflineContentService
{
    Task<OfflinePackageResult> DownloadOfflinePackageAsync(
        string languageCode,
        bool includeAudioFiles = false,
        CancellationToken ct = default);

    Task AutoSyncWhenOnlineAsync(string languageCode, CancellationToken ct = default);

    Task<string?> GetCachedNarrationTextAsync(int poiId, string languageCode);

    Task CacheNarrationScriptAsync(
        int poiId,
        string languageCode,
        string textContent,
        string? audioFileUrl = null,
        int? durationInSeconds = null,
        string? localAudioPath = null);

    Task<OfflinePackageStatus> GetStatusAsync();
}

public record OfflinePackageResult(
    bool Success,
    string Message,
    int PoiCount,
    int ScriptCount,
    int AudioFileCount);

public record OfflinePackageStatus(
    DateTime? LastSyncUtc,
    int PoiCount,
    int ScriptCount,
    int AudioFileCount,
    string LanguageCode);

public class OfflineContentService : IOfflineContentService
{
    private const string KeyLastSyncTicks = "Offline.LastSyncTicks";
    private const string KeyPoiCount = "Offline.PoiCount";
    private const string KeyScriptCount = "Offline.ScriptCount";
    private const string KeyAudioFileCount = "Offline.AudioFileCount";
    private const string KeyLanguage = "Offline.Language";

    private readonly IApiService _apiService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILogger<OfflineContentService> _logger;
    private readonly HttpClient _downloadClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public OfflineContentService(
        IApiService apiService,
        LocalPOIDatabase localDb,
        ILogger<OfflineContentService> logger)
    {
        _apiService = apiService;
        _localDb = localDb;
        _logger = logger;
    }

    public async Task<OfflinePackageResult> DownloadOfflinePackageAsync(
        string languageCode,
        bool includeAudioFiles = false,
        CancellationToken ct = default)
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return new OfflinePackageResult(
                false,
                "Không có mạng Internet để tải gói offline.",
                0,
                0,
                0);
        }

        try
        {
            var lang = NormalizeLanguage(languageCode);
            var pois = await _apiService.GetAllPOIsAsync();

            if (pois.Count == 0)
            {
                return new OfflinePackageResult(
                    false,
                    "Không tải được dữ liệu POI từ server.",
                    0,
                    0,
                    0);
            }

            await _localDb.SavePOIsAsync(pois);

            var scriptCount = 0;
            var audioFileCount = 0;

            foreach (var poi in pois)
            {
                ct.ThrowIfCancellationRequested();

                AudioContentResult? audio = null;
                try
                {
                    audio = await _apiService.GetAudioForPOIAsync(poi.Id, lang);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skip audio fetch for POI {PoiId}", poi.Id);
                }

                var narrationText = !string.IsNullOrWhiteSpace(audio?.TextContent)
                    ? audio!.TextContent!
                    : (!string.IsNullOrWhiteSpace(poi.Description)
                        ? $"{poi.Name}. {poi.Description}"
                        : poi.Name);

                string? localAudioPath = null;
                if (includeAudioFiles && !string.IsNullOrWhiteSpace(audio?.AudioFileUrl))
                {
                    localAudioPath = await DownloadAudioFileAsync(
                        audio!.AudioFileUrl!,
                        poi.Id,
                        audio.LanguageCode ?? lang,
                        ct);

                    if (!string.IsNullOrWhiteSpace(localAudioPath))
                        audioFileCount++;
                }

                await _localDb.SaveAudioScriptAsync(
                    poi.Id,
                    audio?.LanguageCode ?? lang,
                    narrationText,
                    audio?.AudioFileUrl,
                    audio?.DurationInSeconds,
                    localAudioPath);

                scriptCount++;
            }

            var now = DateTime.UtcNow;
            Preferences.Set(KeyLastSyncTicks, now.Ticks);
            Preferences.Set(KeyPoiCount, pois.Count);
            Preferences.Set(KeyScriptCount, scriptCount);
            Preferences.Set(KeyAudioFileCount, audioFileCount);
            Preferences.Set(KeyLanguage, lang);

            // Pre-cache OSM tiles cho khu vực Vĩnh Khánh (zoom 14-17)
            _ = Task.Run(() => PreCacheMapTilesAsync(ct), ct);

            var message = $"Đã tải offline: {pois.Count} POI, {scriptCount} script"
                        + (audioFileCount > 0 ? $", {audioFileCount} file audio" : "") + ".";

            return new OfflinePackageResult(true, message, pois.Count, scriptCount, audioFileCount);
        }
        catch (OperationCanceledException)
        {
            return new OfflinePackageResult(false, "Đã hủy tải gói offline.", 0, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadOfflinePackageAsync failed");
            return new OfflinePackageResult(false, "Tải gói offline thất bại.", 0, 0, 0);
        }
    }

    public async Task AutoSyncWhenOnlineAsync(string languageCode, CancellationToken ct = default)
    {
        try
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                return;

            var status = await GetStatusAsync();
            if (status.LastSyncUtc.HasValue &&
                DateTime.UtcNow - status.LastSyncUtc.Value < TimeSpan.FromMinutes(30))
            {
                return;
            }

            await DownloadOfflinePackageAsync(languageCode, includeAudioFiles: false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AutoSyncWhenOnlineAsync skipped due to error");
        }
    }

    public Task<string?> GetCachedNarrationTextAsync(int poiId, string languageCode)
        => _localDb.GetCachedNarrationTextAsync(poiId, languageCode);

    public Task CacheNarrationScriptAsync(
        int poiId,
        string languageCode,
        string textContent,
        string? audioFileUrl = null,
        int? durationInSeconds = null,
        string? localAudioPath = null)
        => _localDb.SaveAudioScriptAsync(
            poiId,
            languageCode,
            textContent,
            audioFileUrl,
            durationInSeconds,
            localAudioPath);

    public async Task<OfflinePackageStatus> GetStatusAsync()
    {
        var ticks = Preferences.Get(KeyLastSyncTicks, 0L);
        DateTime? lastSync = ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;

        var poiCount = Preferences.Get(KeyPoiCount, -1);
        if (poiCount < 0)
            poiCount = await _localDb.GetCachedPoiCountAsync();

        var scriptCount = Preferences.Get(KeyScriptCount, -1);
        if (scriptCount < 0)
            scriptCount = await _localDb.GetAudioScriptCountAsync();

        var audioFileCount = Preferences.Get(KeyAudioFileCount, 0);
        var languageCode = Preferences.Get(KeyLanguage, "vi");

        return new OfflinePackageStatus(
            lastSync,
            poiCount,
            scriptCount,
            audioFileCount,
            languageCode);
    }


    private async Task<string?> DownloadAudioFileAsync(
        string audioFileUrl,
        int poiId,
        string languageCode,
        CancellationToken ct)
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

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(fullPath, bytes, ct);
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Download audio file failed for POI {PoiId}", poiId);
            return null;
        }
    }

    /// <summary>Pre-download OSM tiles cho khu vực Vĩnh Khánh (zoom 14-17) vào FileCache.</summary>
    private async Task PreCacheMapTilesAsync(CancellationToken ct)
    {
        // Bounding box khu vực Vĩnh Khánh, Quận 4, TP.HCM
        const double minLat = 10.758, maxLat = 10.765;
        const double minLon = 106.699, maxLon = 106.710;

        var tileCacheDir = Path.Combine(FileSystem.CacheDirectory, "osm_tiles");
        Directory.CreateDirectory(tileCacheDir);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VKStreetFood/1.0 (offline-cache)");

        int downloaded = 0, skipped = 0;
        for (int zoom = 14; zoom <= 17; zoom++)
        {
            var (x0, y0) = LatLonToTileXY(maxLat, minLon, zoom);
            var (x1, y1) = LatLonToTileXY(minLat, maxLon, zoom);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (ct.IsCancellationRequested) return;

                    // FileCache path: {dir}/{zoom}/{x}/{y}.png
                    var tilePath = Path.Combine(tileCacheDir, zoom.ToString(), x.ToString(), $"{y}.png");
                    if (File.Exists(tilePath) && new FileInfo(tilePath).Length > 0)
                    { skipped++; continue; }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
                        var url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
                        var bytes = await http.GetByteArrayAsync(url, ct);
                        await File.WriteAllBytesAsync(tilePath, bytes, ct);
                        downloaded++;
                        await Task.Delay(100, ct); // Respect OSM rate limit
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Skip tile {z}/{x}/{y}", zoom, x, y);
                    }
                }
            }
        }
        _logger.LogInformation("[TileCache] Pre-cached {d} tiles, skipped {s}", downloaded, skipped);
    }

    private static (int x, int y) LatLonToTileXY(double lat, double lon, int zoom)
    {
        int n = (int)Math.Pow(2, zoom);
        int x = (int)((lon + 180.0) / 360.0 * n);
        double latRad = lat * Math.PI / 180.0;
        int y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (x, y);
    }

    private static string NormalizeLanguage(string languageCode)
        => string.IsNullOrWhiteSpace(languageCode)
            ? "vi"
            : languageCode.Trim().ToLowerInvariant();

    private static string ToAbsoluteUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUrl = AppSettings.AudioBaseUrl.TrimEnd('/');
        if (url.StartsWith('/'))
            return baseUrl + url;

        return $"{baseUrl}/{url}";
    }
}
