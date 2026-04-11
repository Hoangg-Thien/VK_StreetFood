using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Collections.ObjectModel;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StorageService _storageService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILocationService _locationService;
    private readonly IOfflineContentService _offlineContentService;
    private readonly ITTSService _ttsService;
    private static readonly string[] LanguageCodes = { "vi", "en", "ko" };
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    public string[] LanguageDisplayNames { get; } = { "Tiếng Việt", "English", "한국어" };

    public SettingsViewModel(
        StorageService storageService,
        LocalPOIDatabase localDb,
        ILocationService locationService,
        IOfflineContentService offlineContentService,
        ITTSService ttsService)
    {
        _storageService = storageService;
        _localDb = localDb;
        _locationService = locationService;
        _offlineContentService = offlineContentService;
        _ttsService = ttsService;
        LoadSettings();

        // Sync khi ngôn ngữ được đổi từ trang khác (ví dụ MainMapPage)
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) =>
        {
            _selectedLanguage = LocalizationResourceManager.Instance.CurrentLanguage;
            OnPropertyChanged(nameof(SelectedLanguageDisplayIndex));
            _ = LoadTtsVoicesAsync();
        };

        _ = LoadTtsVoicesAsync();
    }

    [ObservableProperty]
    private string _selectedLanguage = "vi";

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private bool _autoPlayAudio = true;

    [ObservableProperty]
    private double _geofenceRadius = AppSettings.GeofenceRadiusMeters;

    [ObservableProperty]
    private int _locationUpdateInterval = AppSettings.LocationUpdateIntervalSeconds;

    [ObservableProperty]
    private bool _isDownloadingOfflinePackage;

    [ObservableProperty]
    private bool _includeAudioFilesInOfflinePackage;

    [ObservableProperty]
    private string _offlinePackageStatus = "";

    [ObservableProperty]
    private ObservableCollection<TtsVoiceOption> _availableTtsVoices = new();

    [ObservableProperty]
    private TtsVoiceOption? _selectedTtsVoice;

    [ObservableProperty]
    private string _ttsVoiceStatus = "";

    [ObservableProperty]
    private bool _isLoadingTtsVoices;

    public bool HasAvailableTtsVoices => AvailableTtsVoices.Count > 0;

    partial void OnSelectedLanguageChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !string.Equals(LocalizationResourceManager.Instance.CurrentLanguage, value, StringComparison.OrdinalIgnoreCase))
        {
            LocalizationResourceManager.Instance.SetLanguage(value);
            _ = _storageService.SetPreferredLanguageAsync(value);
        }

        OnPropertyChanged(nameof(SelectedLanguageDisplayIndex));
        _ = LoadTtsVoicesAsync();
    }

    partial void OnNotificationsEnabledChanged(bool value)
        => Preferences.Set("NotificationsEnabled", value);

    partial void OnAutoPlayAudioChanged(bool value)
        => Preferences.Set("AutoPlayAudio", value);

    /// <summary>Index trong LanguageDisplayNames để bind Picker.SelectedIndex</summary>
    public int SelectedLanguageDisplayIndex
    {
        get => Array.IndexOf(LanguageCodes, SelectedLanguage);
        set
        {
            if (value >= 0 && value < LanguageCodes.Length)
                SelectedLanguage = LanguageCodes[value];
        }
    }

    void LoadSettings()
    {
        SelectedLanguage = LocalizationResourceManager.Instance.CurrentLanguage;
        NotificationsEnabled = Preferences.Get("NotificationsEnabled", true);
        AutoPlayAudio = Preferences.Get("AutoPlayAudio", true);
        GeofenceRadius = Preferences.Get("GeofenceRadius", AppSettings.GeofenceRadiusMeters);
        LocationUpdateInterval = Preferences.Get("LocationUpdateInterval", AppSettings.LocationUpdateIntervalSeconds);

        if (string.IsNullOrWhiteSpace(OfflinePackageStatus))
            OfflinePackageStatus = L["SettingsOfflineNotDownloaded"];
    }

    [RelayCommand]
    async Task SaveLanguage()
    {
        await _storageService.SetPreferredLanguageAsync(SelectedLanguage);
        // Gọi LocalizationResourceManager để đổi ngôn ngữ toàn app
        LocalizationResourceManager.Instance.SetLanguage(SelectedLanguage);
    }

    [RelayCommand]
    void ToggleNotifications()
    {
        Preferences.Set("NotificationsEnabled", NotificationsEnabled);
    }

    [RelayCommand]
    void ToggleAutoPlay()
    {
        Preferences.Set("AutoPlayAudio", AutoPlayAudio);
    }

    [RelayCommand]
    void SaveGeofenceRadius()
    {
        SaveLocationSettings();
    }

    [RelayCommand]
    void SaveLocationInterval()
    {
        SaveLocationSettings();
    }

    [RelayCommand]
    void SaveLocationSettings()
    {
        Preferences.Set("GeofenceRadius", GeofenceRadius);
        Preferences.Set("LocationUpdateInterval", LocationUpdateInterval);
        _locationService.SetUpdateInterval(LocationUpdateInterval * 1000);
    }

    [RelayCommand]
    async Task DownloadOfflinePackageAsync()
    {
        if (IsDownloadingOfflinePackage)
            return;

        try
        {
            IsDownloadingOfflinePackage = true;

            var result = await _offlineContentService.DownloadOfflinePackageAsync(
                SelectedLanguage,
                IncludeAudioFilesInOfflinePackage);

            await Application.Current!.MainPage!.DisplayAlert(
                result.Success ? L["SettingsOfflineTitle"] : L["Error"],
                result.Message,
                L["OK"]);
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                L["Error"],
                ex.Message,
                L["OK"]);
        }
        finally
        {
            IsDownloadingOfflinePackage = false;
        }
    }

    [RelayCommand]
    async Task RefreshOfflineStatusAsync()
    {
        try
        {
            var status = await _offlineContentService.GetStatusAsync();
            var last = status.LastSyncUtc?.ToLocalTime().ToString("dd/MM HH:mm") ?? L["SettingsOfflineNeverSynced"];

        }
        catch
        {
            OfflinePackageStatus = L["SettingsOfflineStatusReadError"];
        }
    }

    [RelayCommand]
    async Task OpenTtsSettings()
    {
#if ANDROID
        try
        {
            var intent = new global::Android.Content.Intent(
                global::Android.Speech.Tts.TextToSpeech.Engine.ActionInstallTtsData);
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            global::Android.App.Application.Context.StartActivity(intent);
        }
        catch
        {
            await Application.Current!.MainPage!.DisplayAlert(
                L["SettingsTtsTitle"],
                L["SettingsTtsOpenErrorMessage"],
                L["OK"]);
        }
#else
        await Application.Current!.MainPage!.DisplayAlert(
            L["SettingsTtsTitle"],
            L["SettingsTtsAndroidOnlyMessage"],
            L["OK"]);
#endif
    }

    [RelayCommand]
    async Task SaveSelectedTtsVoiceAsync()
    {
        if (SelectedTtsVoice == null)
            return;

        var ok = await _ttsService.SetPreferredVoiceAsync(SelectedTtsVoice.Id, SelectedLanguage);
        TtsVoiceStatus = ok
            ? string.Format(
                CultureInfo.CurrentCulture,
                L["SettingsTtsVoiceSelectedFormat"],
                SelectedTtsVoice.DisplayName)
            : L["SettingsTtsVoiceSaveFailed"];
    }

    [RelayCommand]
    async Task RefreshTtsVoicesAsync()
    {
        await LoadTtsVoicesAsync();
    }

    [RelayCommand]
    async Task ClearCache()
    {
        bool confirm = await Application.Current!.MainPage!.DisplayAlert(
            L["SettingsClearCache"],
            L["SettingsClearCacheConfirm"],
            L["OK"],
            L["Cancel"]);

        if (confirm)
        {
            // Xóa SQLite POI cache
            await _localDb.ClearAsync();
            // Xóa Preferences
            Preferences.Clear();
            LoadSettings(); // reload defaults
            await RefreshOfflineStatusAsync();

            await Application.Current.MainPage.DisplayAlert(
                L["Success"],
                L["SettingsClearCacheDone"],
                L["OK"]);
        }
    }

    [RelayCommand]
    async Task Logout()
    {
        bool confirm = await Application.Current!.MainPage!.DisplayAlert(
            L["SettingsLogout"],
            L["SettingsLogoutConfirm"],
            L["SettingsLogout"],
            L["Cancel"]);

        if (confirm)
        {
            await _storageService.ClearAsync();
            await Shell.Current.GoToAsync("///Welcome");
        }
    }

    private async Task LoadTtsVoicesAsync()
    {
        if (IsLoadingTtsVoices)
            return;

        try
        {
            IsLoadingTtsVoices = true;
            var voices = await _ttsService.GetAvailableVoicesAsync(SelectedLanguage);

            AvailableTtsVoices.Clear();
            foreach (var voice in voices)
                AvailableTtsVoices.Add(voice);

            OnPropertyChanged(nameof(HasAvailableTtsVoices));

            if (voices.Count == 0)
            {
                SelectedTtsVoice = null;
                TtsVoiceStatus = L["SettingsTtsVoiceNoVoices"];
                return;
            }

            var preferredVoiceId = _ttsService.GetPreferredVoiceId(SelectedLanguage);
            SelectedTtsVoice = AvailableTtsVoices
                .FirstOrDefault(v => string.Equals(v.Id, preferredVoiceId, StringComparison.Ordinal))
                ?? AvailableTtsVoices.First();

            TtsVoiceStatus = string.Format(
                CultureInfo.CurrentCulture,
                L["SettingsTtsVoiceReadyFormat"],
                AvailableTtsVoices.Count,
                SelectedTtsVoice.DisplayName);
        }
        catch
        {
            AvailableTtsVoices.Clear();
            SelectedTtsVoice = null;
            OnPropertyChanged(nameof(HasAvailableTtsVoices));
            TtsVoiceStatus = L["SettingsTtsVoiceLoadFailed"];
        }
        finally
        {
            IsLoadingTtsVoices = false;
        }
    }
}
