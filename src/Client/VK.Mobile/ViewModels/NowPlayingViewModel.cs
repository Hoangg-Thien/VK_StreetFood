using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

[QueryProperty(nameof(PoiName), "poiName")]
[QueryProperty(nameof(PoiCategory), "poiCategory")]
[QueryProperty(nameof(PoiImage), "poiImage")]
[QueryProperty(nameof(AudioText), "audioText")]
[QueryProperty(nameof(Language), "language")]
public partial class NowPlayingViewModel : ObservableObject
{
    public static event EventHandler? AutoCloseRequested;
    public static void RequestAutoClose() => AutoCloseRequested?.Invoke(null, EventArgs.Empty);

    private readonly ITTSService _ttsService;
    private readonly IAudioService _audioService;
    private readonly IApiService _apiService;
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
    [ObservableProperty] private string _poiAddress = "District 4, HCMC";
    [ObservableProperty] private string _poiDistance = string.Empty;
    [ObservableProperty] private bool _hasDistance;
    [ObservableProperty] private string _audioText = string.Empty;
    [ObservableProperty] private string _language = "vi";
    [ObservableProperty] private bool _isPlaying = true;
    [ObservableProperty] private double _progressRatio = 0;
    [ObservableProperty] private string _elapsedText = "0:00";
    [ObservableProperty] private string _totalText = "0:00";

    // Up Next POI
    [ObservableProperty] private string _nextPoiName = string.Empty;
    [ObservableProperty] private string _nextPoiSubtitle = string.Empty;
    [ObservableProperty] private string _nextPoiImage = string.Empty;
    [ObservableProperty] private string _nextPoiDistance = string.Empty;
    [ObservableProperty] private bool _hasNextPoi;

    private IDispatcherTimer? _timer;
    private int _elapsedSeconds = 0;
    private int _totalSeconds = 0;
    private CancellationTokenSource? _ttsCts;

    public NowPlayingViewModel(
        ITTSService ttsService,
        IAudioService audioService,
        IApiService apiService,
        IOfflineContentService offlineContentService)
    {
        _ttsService = ttsService;
        _audioService = audioService;
        _apiService = apiService;
        _offlineContentService = offlineContentService;
        _audioService.PlaybackCompleted += OnAudioServicePlaybackCompleted;
    }

    public void Initialize(int poiId, string poiName, string poiCategory, string poiImage,
                           string audioText, string language,
                           string poiAddress = "", string poiDistance = "",
                           POIModel? nextPoiModel = null, string audioFileUrl = "")
    {
        PoiId = poiId;
        PoiName = poiName;
        PoiCategory = poiCategory;
        PoiImage = poiImage;
        PoiAddress = string.IsNullOrEmpty(poiAddress) ? "District 4, HCMC" : poiAddress;
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
        _usingAudioService = false; // reset; set to true in StartPlayingAsync if URL available
    }

    private static string FormatWalk(double? km) => km switch
    {
        null or 0 => "",
        _ => $"{(int)Math.Ceiling(km.Value * 12)} min walk"
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

    public Task StartPlayingAsync()
    {
        _ttsCts?.Cancel();
        _ttsCts?.Dispose();
        _ttsCts = null;
        _elapsedSeconds = 0;
        IsPlaying = true;
        UpdateProgress();
        StartTimer();

        if (!string.IsNullOrWhiteSpace(_audioFileUrl))
        {
            // Tier 1: phát file MP3 từ server
            _usingAudioService = true;
            _ = _audioService.PlayAudioAsync(_audioFileUrl, PoiId);
        }
        else
        {
            // Fallback: device TTS — path cũ không thay đổi
            _usingAudioService = false;
            _ttsCts = new CancellationTokenSource();
            var token = _ttsCts.Token;
            var text = AudioText;
            var lang = Language;
            _ = _ttsService.SpeakTextAsync(text, lang, token);
        }

        return Task.CompletedTask;
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
        StopTimer();

        // Lấy audio text cho POI này
        string audioText;
        string nextAudioFileUrl = "";
        try
        {
            var audio = await _apiService.GetAudioForPOIAsync(next.Id, Language);
            if (!string.IsNullOrWhiteSpace(audio?.TextContent))
            {
                audioText = audio!.TextContent!;
                nextAudioFileUrl = audio.AudioFileUrl ?? "";
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
                    : (string.IsNullOrWhiteSpace(next.Description) ? next.Name : $"{next.Name}. {next.Description}");
            }
        }
        catch
        {
            var cached = await _offlineContentService.GetCachedNarrationTextAsync(next.Id, Language);
            audioText = !string.IsNullOrWhiteSpace(cached)
                ? cached
                : (string.IsNullOrWhiteSpace(next.Description) ? next.Name : $"{next.Name}. {next.Description}");
        }

        // Re-initialize với POI mới — _allPois vẫn còn, tự tính next tiếp theo
        Initialize(next.Id, next.Name ?? string.Empty, next.CategoryName ?? string.Empty,
                   next.ImageUrl ?? string.Empty, audioText, Language,
                   next.Address ?? string.Empty,
                   next.DistanceKm.HasValue
                       ? (next.DistanceKm < 0.1 ? $"{next.DistanceKm.Value * 1000:F0}m away" : $"{next.DistanceKm.Value:F1} km away")
                       : string.Empty,
                   audioFileUrl: nextAudioFileUrl);

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
            }
        };
        _timer.Start();
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
        });
    }
}
