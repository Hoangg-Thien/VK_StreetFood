using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class POIDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IApiService _apiService;
    private readonly ITTSService _ttsService;
    private readonly IOfflineContentService _offlineContentService;
    private readonly LocalPOIDatabase _localDb;
    private readonly StorageService _storageService;
    private readonly ILogger<POIDetailViewModel> _logger;
    private CancellationTokenSource? _ttsCts;
    private IDispatcherTimer? _progressTimer;
    private int _elapsedSeconds;
    private int _totalSeconds;
    private string _fullText = string.Empty;

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
        if (value) StartProgressTimer();
        else StopProgressTimer();
    }

    partial void OnSelectedAudioChanged(AudioInfo? value)
    {
        AudioTranscript = value?.TextContent ?? string.Empty;
        HasTranscript = !string.IsNullOrWhiteSpace(AudioTranscript);
        // Estimate duration from word count (~130 wpm)
        var words = AudioTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        _totalSeconds = Math.Max(10, words * 60 / 130);
        AudioDurationText = FormatTime(_totalSeconds);
        AudioPositionRatio = 0;
        AudioPositionText = "0:00";
        _elapsedSeconds = 0;
        OnPropertyChanged(nameof(AudioStatusText));
    }

    public POIDetailViewModel(
        IApiService apiService,
        ITTSService ttsService,
        IOfflineContentService offlineContentService,
        LocalPOIDatabase localDb,
        StorageService storageService,
        ILogger<POIDetailViewModel> logger)
    {
        _apiService = apiService;
        _ttsService = ttsService;
        _offlineContentService = offlineContentService;
        _localDb = localDb;
        _storageService = storageService;
        _logger = logger;
    }

    private async Task<POIDetailModel?> BuildOfflineDetailAsync(int poiId, string languageCode)
    {
        var cachedPois = await _localDb.GetCachedPOIsAsync();
        var poi = cachedPois.FirstOrDefault(p => p.Id == poiId);
        if (poi == null) return null;

        var text = await _offlineContentService.GetCachedNarrationTextAsync(poiId, languageCode)
                   ?? (!string.IsNullOrWhiteSpace(poi.Description)
                        ? $"{poi.Name}. {poi.Description}"
                        : poi.Name);

        return new POIDetailModel
        {
            Id = poi.Id,
            Name = poi.Name,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            Address = poi.Address,
            ImageUrl = poi.ImageUrl,
            CategoryName = poi.CategoryName,
            AverageRating = poi.AverageRating,
            TotalRatings = poi.TotalRatings,
            Tags = poi.Tags,
            Audio = new AudioInfo
            {
                LanguageCode = languageCode,
                TextContent = text
            }
        };
    }

    // ── Progress timer (fake elapsed based on word-count estimate) ────────
    private void StartProgressTimer()
    {
        StopProgressTimer();
        _progressTimer = Application.Current!.Dispatcher.CreateTimer();
        _progressTimer.Interval = TimeSpan.FromSeconds(1);
        _progressTimer.Tick += (_, _) =>
        {
            if (!IsPlayingAudio) { StopProgressTimer(); return; }
            _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 1);
            AudioPositionRatio = _totalSeconds > 0 ? (double)_elapsedSeconds / _totalSeconds : 0;
            AudioPositionText = FormatTime(_elapsedSeconds);
            OnPropertyChanged(nameof(AudioStatusText));
            if (_elapsedSeconds >= _totalSeconds)
            {
                IsPlayingAudio = false;
                StopProgressTimer();
                _ttsCts?.Cancel();
            }
        };
        _progressTimer.Start();
    }

    private void StopProgressTimer()
    {
        _progressTimer?.Stop();
        _progressTimer = null;
    }

    private static string FormatTime(int totalSeconds)
    {
        var m = totalSeconds / 60;
        var s = totalSeconds % 60;
        return $"{m}:{s:D2}";
    }

    // Returns the portion of fullText starting at the word that corresponds to startSeconds
    private static string GetTextFromPosition(string fullText, int startSeconds, int totalSeconds)
    {
        if (startSeconds <= 0 || totalSeconds <= 0) return fullText;
        var words = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return fullText;
        var startWord = (int)((double)startSeconds / totalSeconds * words.Length);
        startWord = Math.Clamp(startWord, 0, words.Length - 1);
        return string.Join(' ', words.Skip(startWord));
    }

    [RelayCommand]
    private async Task SeekAudio(double ratio)
    {
        if (_totalSeconds <= 0) return;
        _elapsedSeconds = (int)(ratio * _totalSeconds);
        AudioPositionRatio = ratio;
        AudioPositionText = FormatTime(_elapsedSeconds);
        // If currently playing, restart TTS from the new position
        if (IsPlayingAudio)
        {
            _ttsCts?.Cancel();
            await _ttsService.StopAsync();
            IsPlayingAudio = false;
            StopProgressTimer();
            await ToggleAudioAsync();
        }
    }

    // IQueryAttributable: called by Shell when navigating with { "poiId", id }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("poiId", out var val))
        {
            int id = val is int i ? i : int.TryParse(val?.ToString(), out var parsed) ? parsed : 0;
            if (id > 0) _ = LoadPOIDetailAsync(id);
        }
    }

    partial void OnPoiChanged(POIDetailModel? value) { }

    private async Task LoadPOIDetailAsync(int poiId)
    {
        try
        {
            IsLoading = true;

            var language = await _storageService.GetPreferredLanguageAsync() ?? "vi";
            SelectedLanguage = language;

            POIDetailModel? detail = null;
            try
            {
                detail = await _apiService.GetPOIDetailAsync(poiId, language);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API detail failed, trying offline cache");
            }

            if (detail == null)
                detail = await BuildOfflineDetailAsync(poiId, language);

            if (detail != null)
            {
                Poi = detail;

                // API trả về single "audio" (đúng language), không phải list
                // AudioContents luôn rỗng → dùng Audio trực tiếp
                SelectedAudio = detail.AudioContents.FirstOrDefault(a => a.LanguageCode == language)
                             ?? detail.Audio;

                if (string.IsNullOrWhiteSpace(SelectedAudio?.TextContent))
                {
                    var cachedText = await _offlineContentService.GetCachedNarrationTextAsync(poiId, language);
                    if (!string.IsNullOrWhiteSpace(cachedText))
                    {
                        SelectedAudio = new AudioInfo
                        {
                            LanguageCode = language,
                            TextContent = cachedText
                        };
                    }
                }

                if (!string.IsNullOrWhiteSpace(SelectedAudio?.TextContent))
                {
                    await _offlineContentService.CacheNarrationScriptAsync(
                        poiId,
                        language,
                        SelectedAudio.TextContent!,
                        SelectedAudio.AudioFileUrl,
                        SelectedAudio.DurationSeconds);
                }

                // Check if favorite
                var touristId = await _storageService.GetTouristIdAsync();
                if (touristId != null)
                {
                    try
                    {
                        var favorites = await _apiService.GetFavoritesAsync(touristId.Value);
                        IsFavorite = favorites.Any(f => f.Id == poiId);
                    }
                    catch
                    {
                        IsFavorite = false;
                    }
                }

                // Track view event
                if (touristId != null)
                {
                    try
                    {
                        await _apiService.TrackEventAsync(touristId, poiId, "view", language);
                    }
                    catch
                    {
                        // offline mode, ignore analytics failure
                    }
                }
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    "Offline",
                    "Không có dữ liệu POI trong máy. Hãy tải gói offline khi có mạng.",
                    "OK");
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
    private async Task ToggleAudioAsync()
    {
        if (IsPlayingAudio)
        {
            // Pause: stop TTS + timer, keep elapsed
            _ttsCts?.Cancel();
            await _ttsService.StopAsync();
            IsPlayingAudio = false;
            StopProgressTimer();
        }
        else
        {
            // Play/Resume
            var text = SelectedAudio?.TextContent;
            System.Diagnostics.Debug.WriteLine($"[POIDetail] ToggleAudio: SelectedAudio={SelectedAudio != null}, TextContent={text?.Length ?? 0} chars");
            if (string.IsNullOrWhiteSpace(text))
                text = Poi != null ? $"{Poi.Name}. {Poi.Description}" : string.Empty;
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Final text for TTS ({text?.Length ?? 0} chars): {text?[..Math.Min(60, text?.Length ?? 0)]}");
            if (string.IsNullOrWhiteSpace(text))
            {
                await Shell.Current.DisplayAlert("Thông báo", "Không có nội dung âm thanh cho điểm này", "OK");
                return;
            }

            // Store full text so skip/seek can restart from any position
            _fullText = text;

            // Recalculate total duration only on a fresh start (no prior seek/skip)
            if (_elapsedSeconds == 0)
            {
                var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                _totalSeconds = Math.Max(30, wordCount * 60 / 130);
                AudioDurationText = FormatTime(_totalSeconds);
                AudioPositionRatio = 0;
                AudioPositionText = "0:00";
            }

            _ttsCts?.Cancel();
            _ttsCts = new CancellationTokenSource();
            var token = _ttsCts.Token;
            var lang = SelectedLanguage;

            // Setting IsPlayingAudio = true already triggers StartProgressTimer via OnIsPlayingAudioChanged
            IsPlayingAudio = true;

            // Speak from the current seek position (sub-text starting at _elapsedSeconds)
            var speakText = GetTextFromPosition(_fullText, _elapsedSeconds, _totalSeconds);
            _ = _ttsService.SpeakTextAsync(speakText, lang, token)
                           .ContinueWith(t =>
                           {
                               if (t.Exception != null)
                                   System.Diagnostics.Debug.WriteLine($"[TTS] Exception: {t.Exception.GetBaseException().Message}");
                           }, TaskContinuationOptions.OnlyOnFaulted);

            // Track in background (don't block TTS)
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId != null && Poi != null)
                _ = _apiService.TrackEventAsync(touristId, Poi.Id, "audio_play", lang);
        }
    }

    [RelayCommand]
    private async Task SkipForwardAudio()
    {
        _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 5);
        AudioPositionRatio = _totalSeconds > 0 ? (double)_elapsedSeconds / _totalSeconds : 0;
        AudioPositionText = FormatTime(_elapsedSeconds);
        OnPropertyChanged(nameof(AudioStatusText));
        // If playing, cancel current TTS and restart from the new position
        if (IsPlayingAudio)
        {
            _ttsCts?.Cancel();
            await _ttsService.StopAsync();
            IsPlayingAudio = false;
            StopProgressTimer();
            await ToggleAudioAsync();
        }
    }

    [RelayCommand]
    private async Task SkipBackAudio()
    {
        _elapsedSeconds = Math.Max(0, _elapsedSeconds - 5);
        AudioPositionRatio = _totalSeconds > 0 ? (double)_elapsedSeconds / _totalSeconds : 0;
        AudioPositionText = FormatTime(_elapsedSeconds);
        OnPropertyChanged(nameof(AudioStatusText));
        // If playing, cancel current TTS and restart from the new position
        if (IsPlayingAudio)
        {
            _ttsCts?.Cancel();
            await _ttsService.StopAsync();
            IsPlayingAudio = false;
            StopProgressTimer();
            await ToggleAudioAsync();
        }
    }

    [RelayCommand]
    private async Task PlayAudioAsync()
    {
        await ToggleAudioAsync();
    }

    [RelayCommand]
    private async Task StopAudioAsync()
    {
        _ttsCts?.Cancel();
        await _ttsService.StopAsync();
        IsPlayingAudio = false;
        _elapsedSeconds = 0;
        AudioPositionRatio = 0;
        AudioPositionText = "0:00";
        StopProgressTimer();
        OnPropertyChanged(nameof(AudioStatusText));
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId == null || Poi == null)
            {
                await Shell.Current.DisplayAlert("Thông báo", "Vui lòng đăng ký tài khoản để sử dụng tính năng yêu thích.", "OK");
                return;
            }

            bool success;
            if (IsFavorite)
            {
                success = await _apiService.RemoveFavoriteAsync(touristId.Value, Poi.Id);
                if (success) IsFavorite = false;
                else await Shell.Current.DisplayAlert("Lỗi", "Không thể xóa khỏi yêu thích.", "OK");
            }
            else
            {
                success = await _apiService.AddFavoriteAsync(touristId.Value, Poi.Id);
                if (success) IsFavorite = true;
                else await Shell.Current.DisplayAlert("Lỗi", "Không thể thêm vào yêu thích.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite");
            await Shell.Current.DisplayAlert("Lỗi", $"Lỗi kết nối: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task SubmitRatingAsync(string ratingStr)
    {
        if (!int.TryParse(ratingStr, out int rating) || rating < 1) return;
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
        _ttsCts?.Cancel();
        StopProgressTimer();
        await _ttsService.StopAsync();
        await Shell.Current.GoToAsync("..");
    }
}
