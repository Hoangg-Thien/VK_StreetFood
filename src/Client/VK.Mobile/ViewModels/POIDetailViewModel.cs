using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class POIDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IApiService _apiService;
    private readonly ITTSService _ttsService;
    private readonly IAudioService _audioService;
    private readonly StorageService _storageService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILogger<POIDetailViewModel> _logger;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;
    private CancellationTokenSource? _ttsCts;
    private IDispatcherTimer? _progressTimer;
    private int _elapsedSeconds;
    private int _totalSeconds;
    private string _fullText = string.Empty;
    private bool _usingAudioService;
    private bool _hasTrackedAudioPlayForSession;
    private bool _hasTrackedAudioCompleteForSession;
    private DateTime _audioSessionStartedAtUtc;
    private bool _hasTriggeredMp3Fallback;

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

    public bool IsDragging { get; set; }

    public string AudioStatusText => IsPlayingAudio
        ? L["POIDetailAudioStatusPlaying"]
        : (AudioPositionRatio > 0 ? L["POIDetailAudioStatusPaused"] : L["POIDetailAudioStatusReady"]);

    public string AverageRatingText
        => Poi == null
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, L["POIDetailAverageRatingFormat"], Poi.AverageRating);

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
        _totalSeconds = value?.DurationSeconds is > 0
            ? value.DurationSeconds.Value
            : EstimateDurationFromTranscript(AudioTranscript);
        AudioDurationText = FormatTime(_totalSeconds);
        AudioPositionRatio = 0;
        AudioPositionText = "0:00";
        _elapsedSeconds = 0;
        _hasTrackedAudioPlayForSession = false;
        _hasTrackedAudioCompleteForSession = false;
        _audioSessionStartedAtUtc = DateTime.UtcNow;
        _hasTriggeredMp3Fallback = false;
        OnPropertyChanged(nameof(AudioStatusText));
    }

    public POIDetailViewModel(
        IApiService apiService,
        ITTSService ttsService,
        IAudioService audioService,
        StorageService storageService,
        LocalPOIDatabase localDb,
        ILogger<POIDetailViewModel> logger)
    {
        _apiService = apiService;
        _ttsService = ttsService;
        _audioService = audioService;
        _storageService = storageService;
        _localDb = localDb;
        _logger = logger;
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AudioStatusText));
            OnPropertyChanged(nameof(AverageRatingText));
        };
        _audioService.PlaybackCompleted += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => _ = HandlePlaybackCompletedAsync());
        _audioService.PlaybackError += (_, error) =>
            MainThread.BeginInvokeOnMainThread(() => _ = HandlePlaybackErrorAsync(error));
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
            if (IsDragging) return;

            if (_usingAudioService)
            {
                var realDur = (int)_audioService.Duration;
                if (realDur > 0)
                    _totalSeconds = realDur;

                var currentPos = (int)_audioService.CurrentPosition;
                _elapsedSeconds = _totalSeconds > 0
                    ? Math.Min(_totalSeconds, currentPos)
                    : Math.Max(0, currentPos);
            }
            else
            {
                _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 1);
            }

            UpdateAudioProgressUi();
            OnPropertyChanged(nameof(AudioStatusText));
            if (_totalSeconds > 0 && _elapsedSeconds >= _totalSeconds)
            {
                IsPlayingAudio = false;
                StopProgressTimer();
                _ttsCts?.Cancel();
                _ = TrackAudioCompleteIfNeededAsync();
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

    private void UpdateAudioProgressUi()
    {
        AudioPositionRatio = _totalSeconds > 0 ? (double)_elapsedSeconds / _totalSeconds : 0;
        AudioPositionText = FormatTime(_elapsedSeconds);
        AudioDurationText = FormatTime(_totalSeconds);
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
        UpdateAudioProgressUi();
        if (IsPlayingAudio)
        {
            if (_usingAudioService)
                await _audioService.SeekAsync(_elapsedSeconds);
            else
            {
                RestartTtsFromCurrentPosition();
            }
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

    partial void OnPoiChanged(POIDetailModel? value)
    {
        OnPropertyChanged(nameof(AverageRatingText));
    }

    private async Task LoadPOIDetailAsync(int poiId)
    {
        try
        {
            IsLoading = true;

            var language = LocalizationResourceManager.Instance.CurrentLanguage;
            if (string.IsNullOrWhiteSpace(language))
            {
                language = await _storageService.GetPreferredLanguageAsync() ?? "vi";
            }
            SelectedLanguage = language;

            POIDetailModel? detail = null;

            // Thử lấy từ API nếu online
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                detail = await _apiService.GetPOIDetailAsync(poiId, language);
            }

            // Offline fallback: dùng dữ liệu cache
            if (detail == null)
            {
                var cached = await _localDb.GetCachedPOIsAsync(language);
                var poi = cached.FirstOrDefault(p => p.Id == poiId);
                if (poi != null)
                {
                    detail = new POIDetailModel
                    {
                        Id = poi.Id,
                        Name = poi.Name,
                        Description = poi.Description,
                        Address = poi.Address,
                        Latitude = poi.Latitude,
                        Longitude = poi.Longitude,
                        ImageUrl = poi.ImageUrl,
                        CategoryName = poi.CategoryName,
                        AverageRating = poi.AverageRating,
                        ViewCount = poi.ViewCount,
                        Tags = poi.Tags,
                    };

                    // Lấy audio script từ cache
                    var script = await _localDb.GetAudioScriptAsync(poiId, language);
                    if (script != null)
                    {
                        detail.Audio = new AudioInfo
                        {
                            LanguageCode = script.LanguageCode,
                            TextContent = script.TextContent,
                            AudioFileUrl = script.AudioFileUrl,
                            DurationSeconds = script.DurationInSeconds
                        };
                    }
                }
            }

            if (detail != null)
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    await HydrateAndPreloadAllAudioLanguagesAsync(detail, poiId, language);
                }

                Poi = detail;

                // API trả về single "audio" (đúng language), không phải list
                // AudioContents luôn rỗng → dùng Audio trực tiếp
                SelectedAudio = detail.AudioContents.FirstOrDefault(a =>
                                   string.Equals(a.LanguageCode, language, StringComparison.OrdinalIgnoreCase));

                if (SelectedAudio == null
                    && detail.Audio != null
                    && string.Equals(detail.Audio.LanguageCode, language, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedAudio = detail.Audio;
                }

                if (SelectedAudio == null)
                {
                    SelectedAudio = new AudioInfo
                    {
                        LanguageCode = language,
                        TextContent = BuildLocalizedFallbackNarration(detail.Name, detail.Description, language),
                        AudioFileUrl = null,
                        DurationSeconds = null
                    };
                }

                // Check if favorite (chỉ khi online)
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    var touristId = await _storageService.GetTouristIdAsync();
                    if (touristId != null)
                    {
                        var favorites = await _apiService.GetFavoritesAsync(touristId.Value, language);
                        IsFavorite = favorites.Any(f => f.Id == poiId);
                    }

                    // Track view event
                    if (touristId != null)
                    {
                        await _apiService.TrackEventAsync(touristId, poiId, "view", language);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POI detail");
            await Shell.Current.DisplayAlert(L["Error"], L["POIDetailLoadFailed"], L["OK"]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task HydrateAndPreloadAllAudioLanguagesAsync(POIDetailModel detail, int poiId, string preferredLanguage)
    {
        var languages = AppSettings.SupportedLanguages
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(NormalizeLanguage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (languages.Length == 0)
            return;

        try
        {
            var audioResults = await Task.WhenAll(
                languages.Select(async lang => new
                {
                    Language = lang,
                    Result = await _apiService.GetAudioForPOIAsync(poiId, lang)
                }));

            var audioByLanguage = new Dictionary<string, AudioInfo>(StringComparer.OrdinalIgnoreCase);

            if (detail.AudioContents != null)
            {
                foreach (var existing in detail.AudioContents.Where(a => a != null))
                {
                    var normalized = NormalizeLanguage(existing.LanguageCode);
                    if (!string.IsNullOrWhiteSpace(existing.AudioFileUrl) || !string.IsNullOrWhiteSpace(existing.TextContent))
                    {
                        existing.LanguageCode = normalized;
                        audioByLanguage[normalized] = existing;
                    }
                }
            }

            if (detail.Audio != null)
            {
                var normalized = NormalizeLanguage(detail.Audio.LanguageCode);
                detail.Audio.LanguageCode = normalized;
                if (!string.IsNullOrWhiteSpace(detail.Audio.AudioFileUrl) || !string.IsNullOrWhiteSpace(detail.Audio.TextContent))
                {
                    audioByLanguage[normalized] = detail.Audio;
                }
            }

            foreach (var item in audioResults)
            {
                var result = item.Result;
                if (result == null)
                    continue;

                var normalized = NormalizeLanguage(result.LanguageCode);
                if (string.IsNullOrWhiteSpace(result.AudioFileUrl) && string.IsNullOrWhiteSpace(result.TextContent))
                    continue;

                audioByLanguage[normalized] = new AudioInfo
                {
                    Id = result.AudioId,
                    LanguageCode = normalized,
                    AudioFileUrl = result.AudioFileUrl,
                    TextContent = result.TextContent,
                    DurationSeconds = result.DurationInSeconds
                };
            }

            detail.AudioContents = audioByLanguage
                .Values
                .OrderBy(a => a.LanguageCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preferredNormalized = NormalizeLanguage(preferredLanguage);
            var preferredAudio = detail.AudioContents.FirstOrDefault(a =>
                string.Equals(a.LanguageCode, preferredNormalized, StringComparison.OrdinalIgnoreCase));

            if (preferredAudio != null)
            {
                detail.Audio = preferredAudio;
            }

            var preloadTasks = detail.AudioContents
                .Where(a => !string.IsNullOrWhiteSpace(a.AudioFileUrl))
                .Select(a => _audioService.PreloadAudioAsync(a.AudioFileUrl!));

            await Task.WhenAll(preloadTasks);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not hydrate/preload multilingual audio for POI {PoiId}", poiId);
        }
    }

    [RelayCommand]
    private async Task ToggleAudioAsync()
    {
        if (IsPlayingAudio)
        {
            if (_usingAudioService)
                await _audioService.PauseAsync();
            else
            {
                _ttsCts?.Cancel();
                await _ttsService.StopAsync();
            }
            IsPlayingAudio = false;
            StopProgressTimer();
        }
        else
        {
            // Nếu đã phát hết, bấm play lại sẽ reset về đầu và phát lại
            if (_elapsedSeconds >= _totalSeconds && _totalSeconds > 0)
            {
                _elapsedSeconds = 0;
                UpdateAudioProgressUi();
            }

            var audioUrl = SelectedAudio?.AudioFileUrl;
            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                _usingAudioService = true;
                IsPlayingAudio = true;
                UpdateAudioProgressUi();
                StartProgressTimer();
                var canResumeCurrent = Poi != null
                    && _audioService.CurrentPOIId == Poi.Id
                    && !string.IsNullOrWhiteSpace(_audioService.CurrentUrl)
                    && _elapsedSeconds > 0
                    && _elapsedSeconds < _totalSeconds;

                if (canResumeCurrent)
                {
                    await _audioService.ResumeAsync();
                }
                else
                {
                    if (_elapsedSeconds == 0)
                    {
                        _totalSeconds = SelectedAudio?.DurationSeconds is > 0
                            ? SelectedAudio.DurationSeconds.Value
                            : EstimateDurationFromTranscript(SelectedAudio?.TextContent);
                        _elapsedSeconds = 0;
                        UpdateAudioProgressUi();
                    }
                    _ = _audioService.PlayAudioAsync(audioUrl, Poi?.Id);
                }
                _audioSessionStartedAtUtc = DateTime.UtcNow;
                _hasTriggeredMp3Fallback = false;
            }
            else
            {
                _usingAudioService = false;
                var text = SelectedAudio?.TextContent;
                if (string.IsNullOrWhiteSpace(text))
                    text = Poi != null
                        ? BuildLocalizedFallbackNarration(Poi.Name, Poi.Description, SelectedLanguage)
                        : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    await Shell.Current.DisplayAlert(L["Error"], L["POIDetailNoAudioContent"], L["OK"]);
                    return;
                }

                _fullText = text;
                if (_elapsedSeconds == 0)
                {
                    _totalSeconds = EstimateDurationFromTranscript(text);
                    _elapsedSeconds = 0;
                    UpdateAudioProgressUi();
                }

                IsPlayingAudio = true;
                UpdateAudioProgressUi();
                StartProgressTimer();
                _ttsCts?.Cancel();
                _ttsCts = new CancellationTokenSource();
                var token = _ttsCts.Token;
                var lang = SelectedLanguage;
                _audioSessionStartedAtUtc = DateTime.UtcNow;
                var speakText = GetTextFromPosition(_fullText, _elapsedSeconds, _totalSeconds);
                _ = _ttsService.SpeakTextAsync(speakText, lang, token)
                               .ContinueWith(t =>
                               {
                                   if (t.Exception != null)
                                       System.Diagnostics.Debug.WriteLine($"[TTS] Exception: {t.Exception.GetBaseException().Message}");
                               }, TaskContinuationOptions.OnlyOnFaulted);
            }

            // Track in background
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId != null && Poi != null && !_hasTrackedAudioPlayForSession)
            {
                _hasTrackedAudioPlayForSession = true;
                _ = _apiService.TrackEventAsync(touristId, Poi.Id, "audio_play", SelectedLanguage);
            }
        }
    }

    private static int EstimateDurationFromTranscript(string? transcript)
    {
        var words = (transcript ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(10, words * 60 / 130);
    }

    private void RestartTtsFromCurrentPosition()
    {
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;
        var speakText = GetTextFromPosition(_fullText, _elapsedSeconds, _totalSeconds);
        _ = _ttsService.SpeakTextAsync(speakText, SelectedLanguage, token)
            .ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"[TTS] Exception: {t.Exception.GetBaseException().Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    [RelayCommand]
    private async Task SkipForwardAudio()
    {
        _elapsedSeconds = Math.Min(_totalSeconds, _elapsedSeconds + 5);
        UpdateAudioProgressUi();
        OnPropertyChanged(nameof(AudioStatusText));
        if (IsPlayingAudio)
        {
            if (_usingAudioService)
                await _audioService.SeekAsync(_elapsedSeconds);
            else
            {
                RestartTtsFromCurrentPosition();
            }
        }
    }

    [RelayCommand]
    private async Task SkipBackAudio()
    {
        _elapsedSeconds = Math.Max(0, _elapsedSeconds - 5);
        UpdateAudioProgressUi();
        OnPropertyChanged(nameof(AudioStatusText));
        if (IsPlayingAudio)
        {
            if (_usingAudioService)
                await _audioService.SeekAsync(_elapsedSeconds);
            else
            {
                RestartTtsFromCurrentPosition();
            }
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
        if (_usingAudioService)
            await _audioService.StopAsync();
        else
            await _ttsService.StopAsync();
        IsPlayingAudio = false;
        _elapsedSeconds = 0;
        _usingAudioService = false;
        _hasTrackedAudioPlayForSession = false;
        _hasTrackedAudioCompleteForSession = false;
        UpdateAudioProgressUi();
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
                await Shell.Current.DisplayAlert(L["Error"], L["POIDetailFavoriteLoginRequired"], L["OK"]);
                return;
            }

            bool success;
            if (IsFavorite)
            {
                success = await _apiService.RemoveFavoriteAsync(touristId.Value, Poi.Id);
                if (success) IsFavorite = false;
                else await Shell.Current.DisplayAlert(L["Error"], L["POIDetailRemoveFavoriteFailed"], L["OK"]);
            }
            else
            {
                success = await _apiService.AddFavoriteAsync(touristId.Value, Poi.Id);
                if (success) IsFavorite = true;
                else await Shell.Current.DisplayAlert(L["Error"], L["POIDetailAddFavoriteFailed"], L["OK"]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite");
            await Shell.Current.DisplayAlert(
                L["Error"],
                string.Format(CultureInfo.CurrentCulture, L["POIDetailConnectionErrorFormat"], ex.Message),
                L["OK"]);
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
                L["POIDetailRatingPromptTitle"],
                L["POIDetailRatingPromptMessage"],
                L["POIDetailRatingSubmit"],
                L["Cancel"]);

            var success = await _apiService.SubmitRatingAsync(
                touristId.Value,
                Poi.Id,
                rating,
                comment);

            if (success)
            {
                await Shell.Current.DisplayAlert(L["Success"], L["POIDetailRatingSuccess"], L["OK"]);
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
        if (_usingAudioService)
            await _audioService.StopAsync();
        else
            await _ttsService.StopAsync();
        await Shell.Current.GoToAsync("..");
    }

    private async Task HandlePlaybackCompletedAsync()
    {
        _elapsedSeconds = _totalSeconds;
        UpdateAudioProgressUi();
        IsPlayingAudio = false;
        StopProgressTimer();
        await TrackAudioCompleteIfNeededAsync();
    }

    private async Task HandlePlaybackErrorAsync(string error)
    {
        if (!_usingAudioService || _hasTriggeredMp3Fallback)
            return;

        _logger.LogWarning("POI detail MP3 playback failed, fallback to TTS. Error: {Error}", error);
        _hasTriggeredMp3Fallback = true;
        _usingAudioService = false;

        var text = SelectedAudio?.TextContent;
        if (string.IsNullOrWhiteSpace(text))
            text = Poi != null
                ? BuildLocalizedFallbackNarration(Poi.Name, Poi.Description, SelectedLanguage)
                : string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            IsPlayingAudio = false;
            StopProgressTimer();
            return;
        }

        _fullText = text;
        if (_elapsedSeconds == 0)
        {
            _totalSeconds = EstimateDurationFromTranscript(text);
            _elapsedSeconds = 0;
            UpdateAudioProgressUi();
        }

        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        IsPlayingAudio = true;
        _audioSessionStartedAtUtc = DateTime.UtcNow;

        try
        {
            var speakText = GetTextFromPosition(_fullText, _elapsedSeconds, _totalSeconds);
            await _ttsService.SpeakTextAsync(speakText, SelectedLanguage, _ttsCts.Token);
            IsPlayingAudio = false;
            StopProgressTimer();
            await TrackAudioCompleteIfNeededAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback TTS failed in POI detail");
            IsPlayingAudio = false;
            StopProgressTimer();
        }
    }

    private async Task TrackAudioCompleteIfNeededAsync()
    {
        if (_hasTrackedAudioCompleteForSession || Poi == null)
            return;

        _hasTrackedAudioCompleteForSession = true;
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId == null)
                return;

            var durationSeconds = _elapsedSeconds > 0
                ? _elapsedSeconds
                : Math.Max(1, (int)(DateTime.UtcNow - _audioSessionStartedAtUtc).TotalSeconds);

            await _apiService.TrackEventAsync(touristId, Poi.Id, "audio_complete", SelectedLanguage, durationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not track audio_complete for POI {PoiId}", Poi?.Id);
        }
    }

    private static string BuildLocalizedFallbackNarration(string? name, string? description, string languageCode)
    {
        var poiName = string.IsNullOrWhiteSpace(name) ? "POI" : name.Trim();
        var normalized = NormalizeLanguage(languageCode);

        return normalized switch
        {
            "en" => $"{poiName}. Discover this famous street food stop in Vinh Khanh.",
            "ko" => $"{poiName}. 빈칸 거리의 대표 길거리 음식 명소입니다.",
            _ => string.IsNullOrWhiteSpace(description) ? poiName : $"{poiName}. {description}"
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
}
