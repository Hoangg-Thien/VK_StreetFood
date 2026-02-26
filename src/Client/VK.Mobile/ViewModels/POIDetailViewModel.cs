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
        await _audioService.StopAsync();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAudioCompleted(object? sender, EventArgs e)
    {
        IsPlayingAudio = false;

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
