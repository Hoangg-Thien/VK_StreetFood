using System.Globalization;
using Microsoft.Extensions.Logging;
using VK.Mobile.Models;
using VK.Mobile.Resources.Strings;

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

public class OfflineContentService : IOfflineContentService, IOfflineSyncService
{
    private const string KeyLastSyncTicks = "Offline.LastSyncTicks";
    private const string KeyPoiCount = "Offline.PoiCount";
    private const string KeyScriptCount = "Offline.ScriptCount";
    private const string KeyAudioFileCount = "Offline.AudioFileCount";
    private const string KeyLanguage = "Offline.Language";

    private readonly IApiService _apiService;
    private readonly LocalPOIDatabase _localDb;
    private readonly IOfflineAudioDownloader _audioDownloader;
    private readonly IMapTileCacheService _mapTileCacheService;
    private readonly IRoutePackageService _routePackageService;
    private readonly ILogger<OfflineContentService> _logger;

    public OfflineContentService(
        IApiService apiService,
        LocalPOIDatabase localDb,
        IOfflineAudioDownloader audioDownloader,
        IMapTileCacheService mapTileCacheService,
        IRoutePackageService routePackageService,
        ILogger<OfflineContentService> logger)
    {
        _apiService = apiService;
        _localDb = localDb;
        _audioDownloader = audioDownloader;
        _mapTileCacheService = mapTileCacheService;
        _routePackageService = routePackageService;
        _logger = logger;
    }

    public async Task<OfflinePackageResult> DownloadOfflinePackageAsync(
        string languageCode,
        bool includeAudioFiles = false,
        CancellationToken ct = default)
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return new OfflinePackageResult(false, GetLocalizedString("OfflineNoInternet", languageCode), 0, 0, 0);
        }

        try
        {
            var lang = NormalizeLanguage(languageCode);
            var pois = await _apiService.GetAllPOIsAsync(languageCode: lang);

            if (pois.Count == 0)
            {
                return new OfflinePackageResult(false, GetLocalizedString("OfflinePoiFetchFailed", lang), 0, 0, 0);
            }

            await _localDb.SavePOIsAsync(pois, lang);

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
                    localAudioPath = await _audioDownloader.DownloadAudioFileAsync(
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

            _ = Task.Run(() => _mapTileCacheService.PreCacheMapTilesAsync(ct), ct);
            var routePackageReady = await _routePackageService.EnsureRoutePackageAsync(pois, ct);

            var culture = ResolveCulture(lang);
            var audioSuffix = audioFileCount > 0
                ? string.Format(culture, GetLocalizedString("OfflineDownloadAudioSuffixFormat", lang), audioFileCount)
                : string.Empty;

            var routeSuffix = routePackageReady
                ? GetLocalizedString("OfflineDownloadRouteSuffix", lang)
                : string.Empty;

            var message = string.Format(
                culture,
                GetLocalizedString("OfflineDownloadSummaryFormat", lang),
                pois.Count,
                scriptCount,
                audioSuffix,
                routeSuffix);

            return new OfflinePackageResult(true, message, pois.Count, scriptCount, audioFileCount);
        }
        catch (OperationCanceledException)
        {
            return new OfflinePackageResult(false, GetLocalizedString("OfflineDownloadCancelled", languageCode), 0, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadOfflinePackageAsync failed");
            return new OfflinePackageResult(false, GetLocalizedString("OfflineDownloadFailed", languageCode), 0, 0, 0);
        }
    }

    public async Task AutoSyncWhenOnlineAsync(string languageCode, CancellationToken ct = default)
    {
        try
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                return;

            var status = await GetStatusAsync();
            if (status.LastSyncUtc.HasValue && DateTime.UtcNow - status.LastSyncUtc.Value < TimeSpan.FromMinutes(30))
                return;

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

        return new OfflinePackageStatus(lastSync, poiCount, scriptCount, audioFileCount, languageCode);
    }

    private static string NormalizeLanguage(string languageCode)
        => string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode.Trim().ToLowerInvariant();

    private static CultureInfo ResolveCulture(string languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        return normalized switch
        {
            "en" => new CultureInfo("en-US"),
            "ko" => new CultureInfo("ko-KR"),
            _ => new CultureInfo("vi-VN")
        };
    }

    private static string GetLocalizedString(string key, string languageCode)
        => AppResources.ResourceManager.GetString(key, ResolveCulture(languageCode)) ?? key;
}
