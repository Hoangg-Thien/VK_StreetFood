using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using VK.Mobile.Models;
using VK.Mobile.Resources.Strings;
using VK.Mobile.ViewModels;
using VK.Mobile.Views;

namespace VK.Mobile.Services;

public interface INarrationCoordinator
{
    Task<bool> OpenNowPlayingForPoiAsync(
        POIModel poi,
        string languageCode,
        IEnumerable<POIModel> allPois,
        bool autoCloseExistingPlayer = true,
        CancellationToken ct = default);
}

public class NarrationCoordinator : INarrationCoordinator
{
    private readonly IApiService _apiService;
    private readonly IOfflineContentService _offlineContentService;
    private readonly IAudioService _audioService;
    private readonly ITTSService _ttsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NarrationCoordinator> _logger;

    public NarrationCoordinator(
        IApiService apiService,
        IOfflineContentService offlineContentService,
        IAudioService audioService,
        ITTSService ttsService,
        IServiceProvider serviceProvider,
        ILogger<NarrationCoordinator> logger)
    {
        _apiService = apiService;
        _offlineContentService = offlineContentService;
        _audioService = audioService;
        _ttsService = ttsService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<bool> OpenNowPlayingForPoiAsync(
        POIModel poi,
        string languageCode,
        IEnumerable<POIModel> allPois,
        bool autoCloseExistingPlayer = true,
        CancellationToken ct = default)
    {
        if (poi == null || poi.Id <= 0)
            return false;

        try
        {
            if (autoCloseExistingPlayer)
            {
                NowPlayingViewModel.RequestAutoClose();
                await Task.Delay(300, ct);
            }

            await _audioService.StopAsync();
            await _ttsService.StopAsync();

            var narration = await ResolveNarrationAsync(poi, languageCode);
            var normalizedLanguage = NormalizeLanguage(languageCode);

            var page = _serviceProvider.GetRequiredService<NowPlayingPage>();
            var vm = (NowPlayingViewModel)page.BindingContext;

            vm.SetAllPois(allPois?.Any() == true ? allPois : new[] { poi });
            vm.Initialize(
                poi.Id,
                poi.Name ?? string.Empty,
                poi.CategoryName ?? string.Empty,
                poi.ImageUrl ?? string.Empty,
                narration.Text,
                normalizedLanguage,
                poi.Address ?? string.Empty,
                FormatDistance(poi.DistanceKm, normalizedLanguage),
                audioFileUrl: narration.AudioFileUrl,
                isFallback: narration.IsFallback);

            if (Shell.Current?.Navigation == null)
            {
                _logger.LogWarning("Cannot open NowPlaying because navigation is not available");
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.Navigation.PushModalAsync(page, animated: true);
            });

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open NowPlaying for POI {PoiId}", poi.Id);
            return false;
        }
    }

    private async Task<(string Text, string AudioFileUrl, bool IsFallback)> ResolveNarrationAsync(
        POIModel poi,
        string languageCode)
    {
        try
        {
            var audioContent = await _apiService.GetAudioForPOIAsync(poi.Id, languageCode);
            if (audioContent != null && !string.IsNullOrWhiteSpace(audioContent.TextContent))
            {
                await _offlineContentService.CacheNarrationScriptAsync(
                    poi.Id,
                    audioContent.LanguageCode,
                    audioContent.TextContent,
                    audioContent.AudioFileUrl,
                    audioContent.DurationInSeconds);

                return (
                    audioContent.TextContent,
                    audioContent.AudioFileUrl ?? string.Empty,
                    audioContent.IsFallback);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "API narration fetch failed for POI {PoiId}", poi.Id);
        }

        var cachedText = await _offlineContentService.GetCachedNarrationTextAsync(poi.Id, languageCode);
        if (!string.IsNullOrWhiteSpace(cachedText))
            return (cachedText, string.Empty, false);

        return (BuildFallbackText(poi, languageCode), string.Empty, false);
    }

    private static string BuildFallbackText(POIModel poi, string languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        return normalized switch
        {
            "en" => $"{poi.Name}. {(string.IsNullOrWhiteSpace(poi.Description)
                ? "A famous street food spot in Vinh Khanh."
                : poi.Description[..Math.Min(300, poi.Description.Length)])}",
            "ko" => $"{poi.Name}. 이 곳은 빈칸의 유명한 길거리 음식 명소입니다.",
            _ => $"{poi.Name}. {(string.IsNullOrWhiteSpace(poi.Description)
                ? "Điểm ẩm thực nổi tiếng tại Vĩnh Khánh."
                : poi.Description[..Math.Min(300, poi.Description.Length)])}"
        };
    }

    private static string NormalizeLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "vi";

        var code = languageCode.Trim().ToLowerInvariant();
        var separatorIndex = code.IndexOfAny(new[] { '-', '_' });
        return separatorIndex > 0 ? code[..separatorIndex] : code;
    }

    private static string FormatDistance(double? km, string languageCode)
    {
        if (km is null or 0)
            return string.Empty;

        var culture = NormalizeLanguage(languageCode) switch
        {
            "en" => new CultureInfo("en-US"),
            "ko" => new CultureInfo("ko-KR"),
            _ => new CultureInfo("vi-VN")
        };

        if (km < 0.1)
        {
            var format = AppResources.ResourceManager.GetString("NowPlayingDistanceMetersAwayFormat", culture)
                         ?? "{0:F0}m away";
            return string.Format(culture, format, km.Value * 1000);
        }

        var kmFormat = AppResources.ResourceManager.GetString("NowPlayingDistanceKmAwayFormat", culture)
                       ?? "{0:F1} km away";
        return string.Format(culture, kmFormat, km.Value);
    }
}
