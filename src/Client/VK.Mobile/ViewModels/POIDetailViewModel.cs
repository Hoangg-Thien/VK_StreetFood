using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

[QueryProperty("Poi", "POI")]
public partial class POIDetailViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly IAudioService _audioService;
    private readonly StorageService _storageService;
    private readonly ILogger<POIDetailViewModel> _logger;
    private IDispatcherTimer? _positionTimer;

    [ObservableProperty]
    private POIDetailModel? _poi;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPlayingAudio;

    [ObservableProperty]
    private string _selectedLanguage = "vi";

    [ObservableProperty]
    private AudioInfo? _selectedAudio;

    // ── Audio progress tracking ──────────────────────────────────
    [ObservableProperty]
    private double _audioPositionRatio = 0;

    [ObservableProperty]
    private string _audioPositionText = "0:00";

    [ObservableProperty]
    private string _audioDurationText = "0:00";

    [ObservableProperty]
    private string _audioTranscript = string.Empty;

    [ObservableProperty]
    private bool _hasTranscript;

    public string AudioStatusText => IsPlayingAudio
        ? "⏸ Đang phát..."
        : (AudioPositionRatio > 0 ? "Đã tạm dừng" : "Nhấn ▶ để nghe");

    partial void OnIsPlayingAudioChanged(bool value)
    {
        OnPropertyChanged(nameof(AudioStatusText));
        if (value) StartPositionTimer();
        else StopPositionTimer();
    }

    partial void OnSelectedAudioChanged(AudioInfo? value)
    {
        AudioTranscript = value?.TextContent ?? string.Empty;
        HasTranscript = !string.IsNullOrWhiteSpace(AudioTranscript);
        var dur = value?.DurationSeconds ?? 0;
        AudioDurationText = FormatTime(dur);
        AudioPositionRatio = 0;
        AudioPositionText = "0:00";
        OnPropertyChanged(nameof(AudioStatusText));
    }

    public POIDetailViewModel(
        IApiService apiService,
        IAudioService audioService,
        StorageService storageService,
        ILogger<POIDetailViewModel> logger)
    {
        _apiService = apiService;
        _audioService = audioService;
        _storageService = storageService;
        _logger = logger;

        _audioService.PlaybackCompleted += OnAudioCompleted;
    }

    // ── Timer helpers ─────────────────────────────────────────────
    private void StartPositionTimer()
    {
        if (_positionTimer != null) return;
        _positionTimer = Application.Current!.Dispatcher.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(500);
        _positionTimer.Tick += (_, _) => UpdateAudioPosition();
        _positionTimer.Start();
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Stop();
        _positionTimer = null;
    }

    private void UpdateAudioPosition()
    {
        var duration = _audioService.Duration;
        var position = _audioService.CurrentPosition;
        if (duration <= 0) return;

        AudioPositionRatio = position / duration;
        AudioPositionText = FormatTime((int)position);
        AudioDurationText = FormatTime((int)duration);
        OnPropertyChanged(nameof(AudioStatusText));
    }

    private static string FormatTime(int totalSeconds)
    {
        var m = totalSeconds / 60;
        var s = totalSeconds % 60;
        return $"{m}:{s:D2}";
    }

    [RelayCommand]
    private void SeekAudio(double ratio)
    {
        // AudioService expose Duration; seek = ratio * duration
        // Plugin.Maui.Audio không có seek nên chỉ update display
        var dur = _audioService.Duration;
        if (dur > 0)
            AudioPositionText = FormatTime((int)(ratio * dur));
    }

    partial void OnPoiChanged(POIDetailModel? value)
    {
        if (value != null)
        {
            _ = LoadPOIDetailAsync(value.Id);
        }
    }

    [RelayCommand]
    private async Task LoadPOIDetailAsync(int poiId)
    {
        try
        {
            IsLoading = true;

            var language = await _storageService.GetPreferredLanguageAsync() ?? "vi";
            SelectedLanguage = language;

            var detail = await _apiService.GetPOIDetailAsync(poiId, language);

            if (detail != null)
            {
                Poi = detail;

                // Select audio for current language
                SelectedAudio = detail.AudioContents.FirstOrDefault(a => a.LanguageCode == language);

                // Check if favorite
                var touristId = await _storageService.GetTouristIdAsync();
                if (touristId != null)
                {
                    var favorites = await _apiService.GetFavoritesAsync(touristId.Value);
                    IsFavorite = favorites.Any(f => f.PointOfInterestId == poiId);
                }

                // Track view event
                if (touristId != null)
                {
                    await _apiService.TrackEventAsync(touristId, poiId, "view", language);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POI detail");
            await Shell.Current.DisplayAlert("Error", "Failed to load POI details", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayAudioAsync()
    {
        try
        {
            if (SelectedAudio?.AudioFileUrl == null)
            {
                await Shell.Current.DisplayAlert("Info", "No audio available", "OK");
                return;
            }

            if (_audioService.IsPlaying)
            {
                await _audioService.PauseAsync();
                IsPlayingAudio = false;
            }
            else
            {
                var fullUrl = SelectedAudio.AudioFileUrl.StartsWith("http")
                    ? SelectedAudio.AudioFileUrl
                    : AppSettings.AudioBaseUrl + SelectedAudio.AudioFileUrl.TrimStart('/');

                var success = await _audioService.PlayAudioAsync(fullUrl);

                if (success)
                {
                    IsPlayingAudio = true;

                    // Track audio play event
                    var touristId = await _storageService.GetTouristIdAsync();
                    if (touristId != null && Poi != null)
                    {
                        await _apiService.TrackEventAsync(
                            touristId,
                            Poi.Id,
                            "audio_play",
                            SelectedLanguage);
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to play audio", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio");
            await Shell.Current.DisplayAlert("Error", "Failed to play audio", "OK");
        }
    }

    [RelayCommand]
    private async Task StopAudioAsync()
    {
        await _audioService.StopAsync();
        IsPlayingAudio = false;
        AudioPositionRatio = 0;
        AudioPositionText = "0:00";
        StopPositionTimer();
        OnPropertyChanged(nameof(AudioStatusText));
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId == null || Poi == null)
                return;

            bool success;
            if (IsFavorite)
            {
                success = await _apiService.RemoveFavoriteAsync(touristId.Value, Poi.Id);
                if (success)
                {
                    IsFavorite = false;
                    await Shell.Current.DisplayAlert("Success", "Removed from favorites", "OK");
                }
            }
            else
            {
                success = await _apiService.AddFavoriteAsync(touristId.Value, Poi.Id);
                if (success)
                {
                    IsFavorite = true;
                    await Shell.Current.DisplayAlert("Success", "Added to favorites", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite");
        }
    }

    [RelayCommand]
    private async Task SubmitRatingAsync(int rating)
    {
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId == null || Poi == null)
                return;

            var comment = await Shell.Current.DisplayPromptAsync(
                "Rating",
                "Optional comment:",
                "Submit",
                "Cancel");

            var success = await _apiService.SubmitRatingAsync(
                touristId.Value,
                Poi.Id,
                rating,
                comment);

            if (success)
            {
                await Shell.Current.DisplayAlert("Success", "Rating submitted", "OK");
                await LoadPOIDetailAsync(Poi.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting rating");
        }
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync(string languageCode)
    {
        SelectedLanguage = languageCode;
        await _storageService.SetPreferredLanguageAsync(languageCode);

        if (Poi != null)
        {
            await LoadPOIDetailAsync(Poi.Id);
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        StopPositionTimer();
        await _audioService.StopAsync();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAudioCompleted(object? sender, EventArgs e)
    {
        IsPlayingAudio = false;
        StopPositionTimer();
        AudioPositionRatio = 0;
        AudioPositionText = "0:00";
        OnPropertyChanged(nameof(AudioStatusText));

        // Track audio complete event
        var touristId = await _storageService.GetTouristIdAsync();
        if (touristId != null && Poi != null)
        {
            await _apiService.TrackEventAsync(
                touristId,
                Poi.Id,
                "audio_complete",
                SelectedLanguage,
                SelectedAudio?.DurationSeconds);
        }
    }
}
