using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

[QueryProperty(nameof(PoiName), "poiName")]
[QueryProperty(nameof(PoiCategory), "poiCategory")]
[QueryProperty(nameof(PoiImage), "poiImage")]
[QueryProperty(nameof(AudioText), "audioText")]
[QueryProperty(nameof(Language), "language")]
public partial class NowPlayingViewModel : ObservableObject, IQueryAttributable
{
    public static event EventHandler? AutoCloseRequested;
    public static void RequestAutoClose() => AutoCloseRequested?.Invoke(null, EventArgs.Empty);
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    private readonly ITTSService _ttsService;
    private readonly IAudioService _audioService;
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly IOfflineContentService _offlineContentService;
    private POIModel? _nextPoiModel;
    private List<POIModel> _allPois = new();

    // Khi API trả về MP3 URL → dùng AudioService; ngược lại dùng TTS
    private string _audioFileUrl = "";
    private bool _usingAudioService;

    public void SetAllPois(IEnumerable<POIModel> pois) => _allPois = pois.ToList();

    [ObservableProperty] private int _poiId;
    [ObservableProperty] private string _poiName = string.Empty;
    [ObservableProperty] private string _poiCategory = string.Empty;
    [ObservableProperty] private string _poiImage = string.Empty;
    [ObservableProperty] private string _poiAddress = string.Empty;
    [ObservableProperty] private string _poiDistance = string.Empty;
    [ObservableProperty] private bool _hasDistance;
    [ObservableProperty] private string _audioText = string.Empty;
    [ObservableProperty] private string _language = "vi";
    [ObservableProperty] private bool _isPlaying = false;
    [ObservableProperty] private double _progressRatio = 0;
    [ObservableProperty] private string _elapsedText = "0:00";
    [ObservableProperty] private string _totalText = "0:00";

    // Up Next POI
    [ObservableProperty] private string _nextPoiName = string.Empty;
    [ObservableProperty] private string _nextPoiSubtitle = string.Empty;
    [ObservableProperty] private string _nextPoiImage = string.Empty;
    [ObservableProperty] private string _nextPoiDistance = string.Empty;
    [ObservableProperty] private bool _hasNextPoi;

    /// <summary>True khi server trả về ngôn ngữ fallback thay vì ngôn ngữ yêu cầu.</summary>
    [ObservableProperty] private bool _isFallback;

    private IDispatcherTimer? _timer;
    private int _elapsedSeconds = 0;
    private int _totalSeconds = 0;
    private CancellationTokenSource? _ttsCts;
    private bool _hasTrackedAudioPlay;
    private bool _hasTrackedAudioComplete;
    private DateTime _playbackStartedAtUtc;
    private bool _shouldAutoplayFromQuery;
    private bool _hasAutoStartedFromQuery;
    private bool _hasTriggeredMp3Fallback;

    public NowPlayingViewModel(
        ITTSService ttsService,
        IAudioService audioService,
        IApiService apiService,
        StorageService storageService,
        IOfflineContentService offlineContentService)
    {
        _ttsService = ttsService;
        _audioService = audioService;
        _apiService = apiService;
        _storageService = storageService;
        _offlineContentService = offlineContentService;
        _audioService.PlaybackCompleted += OnAudioServicePlaybackCompleted;
        _audioService.PlaybackError += OnAudioServicePlaybackError;

        if (string.IsNullOrWhiteSpace(PoiAddress))
            PoiAddress = L["NowPlayingDefaultAddress"];
    }

    public void Initialize(int poiId, string poiName, string poiCategory, string poiImage,
                           string audioText, string language,
                           string poiAddress = "", string poiDistance = "",
                           POIModel? nextPoiModel = null, string audioFileUrl = "",
                           bool isFallback = false)
    {
        PoiId = poiId;
        PoiName = poiName;
        PoiCategory = poiCategory;
        PoiImage = poiImage;
        PoiAddress = string.IsNullOrEmpty(poiAddress) ? L["NowPlayingDefaultAddress"] : poiAddress;
        PoiDistance = poiDistance;
        HasDistance = !string.IsNullOrEmpty(poiDistance);
        Language = language;

        // Nếu caller không truyền nextPoiModel, tìm POI gần POI hiện tại nhất (theo tọa độ)
        if (nextPoiModel != null)
        {
            _nextPoiModel = nextPoiModel;
        }
        else
        {
            var currentPoi = _allPois.FirstOrDefault(p => p.Id == poiId);
            if (currentPoi != null)
            {
                _nextPoiModel = _allPois
                    .Where(p => p.Id != poiId)
                    .OrderBy(p => HaversineKm(currentPoi.Latitude, currentPoi.Longitude, p.Latitude, p.Longitude))
                    .FirstOrDefault();
            }
            else
            {
                // Fallback nếu không tìm thấy tọa độ
                _nextPoiModel = _allPois
                    .Where(p => p.Id != poiId)
                    .OrderBy(p => p.DistanceKm ?? double.MaxValue)
                    .FirstOrDefault();
            }
        }

        NextPoiName = _nextPoiModel?.Name ?? string.Empty;
        NextPoiSubtitle = _nextPoiModel?.CategoryName ?? string.Empty;
        NextPoiImage = _nextPoiModel?.ImageUrl ?? string.Empty;
        NextPoiDistance = FormatWalk(_nextPoiModel?.DistanceKm);
        HasNextPoi = _nextPoiModel != null;
        AudioText = audioText;
        _audioFileUrl = audioFileUrl;
        IsFallback = isFallback;
        _usingAudioService = false; // reset; set to true in StartPlayingAsync if URL available
        IsPlaying = false;
        _hasTrackedAudioPlay = false;
        _hasTrackedAudioComplete = false;
        _playbackStartedAtUtc = DateTime.UtcNow;
        _hasAutoStartedFromQuery = false;
        _hasTriggeredMp3Fallback = false;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("poiId", out var poiVal))
            return;

        var poiId = poiVal is int i
            ? i
            : int.TryParse(poiVal?.ToString(), out var parsed) ? parsed : 0;

        if (poiId <= 0)
            return;

        var requestedLanguage = query.TryGetValue("language", out var langVal)
            ? langVal?.ToString()
            : null;

        _shouldAutoplayFromQuery = query.TryGetValue("autoplay", out var autoVal)
            && (autoVal?.ToString()?.Trim().ToLowerInvariant() is "1" or "true");

        _ = LoadFromPoiQueryAsync(poiId, requestedLanguage);
    }

    private async Task LoadFromPoiQueryAsync(int poiId, string? requestedLanguage)
    {
        try
        {
            var lang = string.IsNullOrWhiteSpace(requestedLanguage)
                ? (LocalizationResourceManager.Instance.CurrentLanguage ?? "vi")
                : requestedLanguage!.Trim().ToLowerInvariant();

            var poi = await _apiService.GetPOIDetailAsync(poiId, lang);
            if (poi == null)
            {
                return;
            }

            var narration = await _offlineContentService.GetCachedNarrationTextAsync(poiId, lang);
            string audioFileUrl = string.Empty;
            bool isFallback = false;

            var audioFromApi = await _apiService.GetAudioForPOIAsync(poiId, lang);
            if (!string.IsNullOrWhiteSpace(audioFromApi?.TextContent))
            {
                narration = audioFromApi!.TextContent;
                audioFileUrl = audioFromApi.AudioFileUrl ?? string.Empty;
                isFallback = audioFromApi.IsFallback;
            }

            if (string.IsNullOrWhiteSpace(narration))
            {
                narration = BuildLocalizedFallbackNarration(poi, lang);
            }

            Initialize(
                poi.Id,
                poi.Name ?? string.Empty,
                poi.CategoryName ?? string.Empty,
                poi.ImageUrl ?? string.Empty,
                narration,
                lang,
                poi.Address ?? string.Empty,
                string.Empty,
                audioFileUrl: audioFileUrl,
                isFallback: isFallback);

            if (_shouldAutoplayFromQuery && !_hasAutoStartedFromQuery && !string.IsNullOrWhiteSpace(AudioText))
            {
                _hasAutoStartedFromQuery = true;
                await StartPlayingAsync();
            }
        }
        catch
        {
            // Keep screen alive even if remote load fails; caller can retry by reopening QR.
        }
    }

    private static string FormatWalk(double? km) => km switch
    {
        null or 0 => "",
        _ => string.Format(
            CultureInfo.CurrentCulture,
            LocalizationResourceManager.Instance["NowPlayingWalkMinutesFormat"],
            (int)Math.Ceiling(km.Value * 12))
    };

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    partial void OnAudioTextChanged(string value)
    {
        // Estimate duration: ~130 words/min speaking rate
        var wordCount = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        _totalSeconds = Math.Max(10, wordCount * 60 / 130);
        TotalText = FormatTime(_totalSeconds);
    }

    public async Task StartPlayingAsync()
    {
        _ttsCts?.Cancel();
        _ttsCts?.Dispose();
        _ttsCts = null;
        _elapsedSeconds = 0;
        IsPlaying = true;
        UpdateProgress();
        _playbackStartedAtUtc = DateTime.UtcNow;
        _ = TrackAudioPlayIfNeededAsync();

        // Tier 1: Pre-generated MP3 → play immediately
        if (!string.IsNullOrWhiteSpace(_audioFileUrl))
        {
            _usingAudioService = true;
            StartTimer();
            _ = _audioService.PlayAudioAsync(_audioFileUrl, PoiId);
            return;
        }

        // Device TTS fallback (local — works offline)
        _usingAudioService = false;
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;
        var text = AudioText;
        var lang = Language;
        StartTimer();
        _ = _ttsService.SpeakTextAsync(text, lang, token);
    }

    // Returns the sub-text starting at the word corresponding to startSeconds
    private string GetTextFromPosition(int startSeconds)
    {
        if (startSeconds <= 0 || _totalSeconds <= 0) return AudioText;
        var words = AudioText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return AudioText;
        var startWord = (int)((double)startSeconds / _totalSeconds * words.Length);
        startWord = Math.Clamp(startWord, 0, words.Length - 1);
        return string.Join(' ', words.Skip(startWord));
    }

    private void RestartTtsFromCurrentPosition()
    {
        _ttsCts?.Cancel();
        _ttsCts?.Dispose();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;
        _ = _ttsService.SpeakTextAsync(GetTextFromPosition(_elapsedSeconds), Language, token);
    }

    [RelayCommand]
    private async Task SkipForwardAsync()
    {
        _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 5);
        UpdateProgress();
        if (IsPlaying)
        {
            if (_usingAudioService)
                await _audioService.SeekAsync(_elapsedSeconds);
            else
                RestartTtsFromCurrentPosition();
        }
    }

    [RelayCommand]
    private async Task SkipBackAsync()
    {
        _elapsedSeconds = Math.Max(0, _elapsedSeconds - 5);
        UpdateProgress();
        if (IsPlaying)
        {
            if (_usingAudioService)
                await _audioService.SeekAsync(_elapsedSeconds);
            else
                RestartTtsFromCurrentPosition();
        }
    }

    [RelayCommand]
    private async Task TogglePlayAsync()
    {
        if (IsPlaying)
        {
            if (_usingAudioService)
                await _audioService.PauseAsync();
            else
            {
                _ttsCts?.Cancel();
                await _ttsService.StopAsync();
            }
            IsPlaying = false;
            StopTimer();
        }
        else
        {
            IsPlaying = true;
            UpdateProgress();
            StartTimer();
            if (_usingAudioService)
                await _audioService.ResumeAsync();
            else
                RestartTtsFromCurrentPosition();
        }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        _ttsCts?.Cancel();
        StopTimer();
        if (_usingAudioService)
            await _audioService.StopAsync();
        else
            await _ttsService.StopAsync();
        await Shell.Current.Navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task PlayNextAsync()
    {
        if (_nextPoiModel == null) return;
        var next = _nextPoiModel;

        // Dừng audio hiện tại
        _ttsCts?.Cancel();
        await _ttsService.StopAsync();
        if (_usingAudioService)
            await _audioService.StopAsync();
        StopTimer();

        // Lấy audio text cho POI này
        string audioText;
        string nextAudioFileUrl = "";
        bool nextIsFallback = false;
        try
        {
            var audio = await _apiService.GetAudioForPOIAsync(next.Id, Language);
            if (!string.IsNullOrWhiteSpace(audio?.TextContent))
            {
                audioText = audio!.TextContent!;
                nextAudioFileUrl = audio.AudioFileUrl ?? "";
                nextIsFallback = audio.IsFallback;
                await _offlineContentService.CacheNarrationScriptAsync(
                    next.Id,
                    audio.LanguageCode,
                    audio.TextContent!,
                    audio.AudioFileUrl,
                    audio.DurationInSeconds);
            }
            else
            {
                var cached = await _offlineContentService.GetCachedNarrationTextAsync(next.Id, Language);
                audioText = !string.IsNullOrWhiteSpace(cached)
                    ? cached
                    : BuildLocalizedFallbackNarration(next, Language);
            }
        }
        catch
        {
            var cached = await _offlineContentService.GetCachedNarrationTextAsync(next.Id, Language);
            audioText = !string.IsNullOrWhiteSpace(cached)
                ? cached
                : BuildLocalizedFallbackNarration(next, Language);
        }

        // Re-initialize với POI mới — _allPois vẫn còn, tự tính next tiếp theo
        Initialize(next.Id, next.Name ?? string.Empty, next.CategoryName ?? string.Empty,
                   next.ImageUrl ?? string.Empty, audioText, Language,
                   next.Address ?? string.Empty,
                   next.DistanceKm.HasValue
                       ? (next.DistanceKm < 0.1
                           ? string.Format(
                               CultureInfo.CurrentCulture,
                               L["NowPlayingDistanceMetersAwayFormat"],
                               next.DistanceKm.Value * 1000)
                           : string.Format(
                               CultureInfo.CurrentCulture,
                               L["NowPlayingDistanceKmAwayFormat"],
                               next.DistanceKm.Value))
                       : string.Empty,
                   audioFileUrl: nextAudioFileUrl,
                   isFallback: nextIsFallback);

        _elapsedSeconds = 0;
        await StartPlayingAsync();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            if (!IsPlaying || IsDragging) return;

            if (_usingAudioService)
            {
                // Poll duration thực từ AudioService (có thể chưa có ngay khi download xong)
                var realDur = (int)_audioService.Duration;
                if (realDur > 0) _totalSeconds = realDur;
                _elapsedSeconds = Math.Min(_totalSeconds, (int)_audioService.CurrentPosition);
            }
            else
            {
                _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 1);
            }

            UpdateProgress();
            if (_elapsedSeconds >= _totalSeconds)
            {
                IsPlaying = false;
                StopTimer();
                _ttsCts?.Cancel(); // no-op nếu AudioService path
                _ = TrackAudioCompleteIfNeededAsync();
            }
        };
        _timer.Start();
    }

    private static string BuildLocalizedFallbackNarration(POIModel poi, string languageCode)
    {
        var normalized = string.IsNullOrWhiteSpace(languageCode)
            ? "vi"
            : languageCode.Trim().ToLowerInvariant();

        return normalized switch
        {
            "en" => $"{poi.Name}. Discover this famous street food stop in Vinh Khanh.",
            "ko" => $"{poi.Name}. 빈칸 거리의 대표 길거리 음식 명소입니다.",
            _ => string.IsNullOrWhiteSpace(poi.Description) ? poi.Name : $"{poi.Name}. {poi.Description}"
        };
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void UpdateProgress()
    {
        ElapsedText = FormatTime(_elapsedSeconds);
        ProgressRatio = _totalSeconds > 0 ? (double)_elapsedSeconds / _totalSeconds : 0;
        TotalText = FormatTime(_totalSeconds);
    }

    /// <summary>Called from code-behind when user drags the slider.</summary>
    public void SeekTo(double ratio)
    {
        _elapsedSeconds = (int)(ratio * _totalSeconds);
        ElapsedText = FormatTime(_elapsedSeconds);
        TotalText = FormatTime(_totalSeconds);
        // Don't set ProgressRatio here – slider already shows the correct value
        if (IsPlaying)
        {
            if (_usingAudioService)
                _ = _audioService.SeekAsync(_elapsedSeconds);
            else
                RestartTtsFromCurrentPosition();
        }
    }

    public bool IsDragging { get; set; }

    private static string FormatTime(int totalSec)
    {
        var m = totalSec / 60;
        var s = totalSec % 60;
        return $"{m}:{s:D2}";
    }

    private void OnAudioServicePlaybackCompleted(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            StopTimer();
            _ = TrackAudioCompleteIfNeededAsync();
        });
    }

    private void OnAudioServicePlaybackError(object? sender, string error)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!_usingAudioService || _hasTriggeredMp3Fallback || string.IsNullOrWhiteSpace(AudioText))
            {
                IsPlaying = false;
                StopTimer();
                return;
            }

            // MP3 online lỗi: fallback sang TTS local để không bị im tiếng.
            _hasTriggeredMp3Fallback = true;
            _usingAudioService = false;
            _elapsedSeconds = 0;
            UpdateProgress();

            _ttsCts?.Cancel();
            _ttsCts?.Dispose();
            _ttsCts = new CancellationTokenSource();

            try
            {
                await _ttsService.SpeakTextAsync(AudioText, Language, _ttsCts.Token);
                IsPlaying = false;
                StopTimer();
                await TrackAudioCompleteIfNeededAsync();
            }
            catch
            {
                IsPlaying = false;
                StopTimer();
            }
        });
    }

    private async Task TrackAudioPlayIfNeededAsync()
    {
        if (_hasTrackedAudioPlay || PoiId <= 0)
            return;

        _hasTrackedAudioPlay = true;
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            await _apiService.TrackEventAsync(touristId, PoiId, "audio_play", Language);
        }
        catch
        {
            // Best effort analytics, never block playback.
        }
    }

    private async Task TrackAudioCompleteIfNeededAsync()
    {
        if (_hasTrackedAudioComplete || PoiId <= 0)
            return;

        _hasTrackedAudioComplete = true;
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            var durationSeconds = _elapsedSeconds > 0
                ? _elapsedSeconds
                : Math.Max(1, (int)(DateTime.UtcNow - _playbackStartedAtUtc).TotalSeconds);

            await _apiService.TrackEventAsync(touristId, PoiId, "audio_complete", Language, durationSeconds);
        }
        catch
        {
            // Best effort analytics, never block playback.
        }
    }
}
