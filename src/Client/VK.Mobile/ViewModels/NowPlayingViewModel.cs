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
    /// <summary>
    /// Được bắn khi một geofence mới trigger, yêu cầu NowPlayingPage đang mở tự đóng.
    /// </summary>
    public static event EventHandler? AutoCloseRequested;
    public static void RequestAutoClose() => AutoCloseRequested?.Invoke(null, EventArgs.Empty);

    private readonly ITTSService _ttsService;

    [ObservableProperty] private string _poiName = string.Empty;
    [ObservableProperty] private string _poiCategory = string.Empty;
    [ObservableProperty] private string _poiImage = string.Empty;
    [ObservableProperty] private string _audioText = string.Empty;
    [ObservableProperty] private string _language = "vi";
    [ObservableProperty] private bool _isPlaying = true;
    [ObservableProperty] private double _progressRatio = 0;
    [ObservableProperty] private string _elapsedText = "0:00";
    [ObservableProperty] private string _totalText = "0:00";

    private IDispatcherTimer? _timer;
    private int _elapsedSeconds = 0;
    private int _totalSeconds = 0;
    private CancellationTokenSource? _ttsCts;

    public NowPlayingViewModel(ITTSService ttsService)
    {
        _ttsService = ttsService;
    }

    public void Initialize(string poiName, string poiCategory, string poiImage, string audioText, string language)
    {
        PoiName = poiName;
        PoiCategory = poiCategory;
        PoiImage = poiImage;
        Language = language;
        AudioText = audioText; // triggers OnAudioTextChanged to recalculate duration
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

        // TTS chạy background — UI progress do timer quản lý độc lập
        var token = _ttsCts.Token;
        var text = AudioText;
        var lang = Language;
        _ = Task.Run(async () =>
        {
            try { await _ttsService.SpeakTextAsync(text, lang, token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NowPlaying] TTS error: {ex.Message}"); }
        });

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
    private async Task StopAsync()
    {
        _ttsCts?.Cancel();
        await _ttsService.StopAsync();
        IsPlaying = false;
        StopTimer();
        _elapsedSeconds = 0;
        UpdateProgress();
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        _ttsCts?.Cancel();
        await _ttsService.StopAsync();
        StopTimer();
        await Shell.Current.Navigation.PopModalAsync();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            if (!IsPlaying) return;
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

    private static string FormatTime(int totalSec)
    {
        var m = totalSec / 60;
        var s = totalSec % 60;
        return $"{m}:{s:D2}";
    }
}
