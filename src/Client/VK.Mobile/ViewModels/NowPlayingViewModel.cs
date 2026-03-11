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
    private readonly IApiService _apiService;
    private POIModel? _nextPoiModel;
    private List<POIModel> _allPois = new();

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

    public NowPlayingViewModel(ITTSService ttsService, IApiService apiService)
    {
        _ttsService = ttsService;
        _apiService = apiService;
    }

    public void Initialize(int poiId, string poiName, string poiCategory, string poiImage,
                           string audioText, string language,
                           string poiAddress = "", string poiDistance = "",
                           POIModel? nextPoiModel = null)
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
        _ttsCts = new CancellationTokenSource();
        _elapsedSeconds = 0;
        IsPlaying = true;
        UpdateProgress();
        StartTimer();

        // Fire TTS directly — do NOT use Task.Run (background thread breaks Android TTS)
        var token = _ttsCts.Token;
        var text = AudioText;
        var lang = Language;
        _ = _ttsService.SpeakTextAsync(text, lang, token);

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void SkipForward()
    {
        _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 5);
        UpdateProgress();
    }

    [RelayCommand]
    private void SkipBack()
    {
        _elapsedSeconds = Math.Max(0, _elapsedSeconds - 5);
        UpdateProgress();
    }

    [RelayCommand]
    private async Task TogglePlayAsync()
    {
        if (IsPlaying)
        {
            // Pause: stop TTS + timer, keep elapsed position (don't reset to 0)
            _ttsCts?.Cancel();
            await _ttsService.StopAsync();
            IsPlaying = false;
            StopTimer();
        }
        else
        {
            // Resume from current elapsed position (timer continues, TTS restarts)
            _ttsCts?.Cancel();
            _ttsCts?.Dispose();
            _ttsCts = new CancellationTokenSource();
            IsPlaying = true;
            UpdateProgress();
            StartTimer();
            var token = _ttsCts.Token;
            var text = AudioText;
            var lang = Language;
            _ = _ttsService.SpeakTextAsync(text, lang, token);
        }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        _ttsCts?.Cancel();
        await _ttsService.StopAsync();
        StopTimer();
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
        try
        {
            var audio = await _apiService.GetAudioForPOIAsync(next.Id, Language);
            audioText = !string.IsNullOrWhiteSpace(audio?.TextContent)
                ? audio!.TextContent!
                : (string.IsNullOrWhiteSpace(next.Description) ? next.Name : $"{next.Name}. {next.Description}");
        }
        catch
        {
            audioText = string.IsNullOrWhiteSpace(next.Description) ? next.Name : $"{next.Name}. {next.Description}";
        }

        // Re-initialize với POI mới — _allPois vẫn còn, tự tính next tiếp theo
        Initialize(next.Id, next.Name ?? string.Empty, next.CategoryName ?? string.Empty,
                   next.ImageUrl ?? string.Empty, audioText, Language,
                   next.Address ?? string.Empty,
                   next.DistanceKm.HasValue
                       ? (next.DistanceKm < 0.1 ? $"{next.DistanceKm.Value * 1000:F0}m away" : $"{next.DistanceKm.Value:F1} km away")
                       : string.Empty);

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
            _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 1);
            UpdateProgress();
            if (_elapsedSeconds >= _totalSeconds)
            {
                IsPlaying = false;
                StopTimer();
                _ttsCts?.Cancel(); // dừng TTS nếu vẫn đang chạy
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
    }

    public bool IsDragging { get; set; }

    private static string FormatTime(int totalSec)
    {
        var m = totalSec / 60;
        var s = totalSec % 60;
        return $"{m}:{s:D2}";
    }
}
