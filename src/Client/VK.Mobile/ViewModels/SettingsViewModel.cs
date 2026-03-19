using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StorageService _storageService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILocationService _locationService;
    private readonly IOfflineContentService _offlineContentService;
    private static readonly string[] LanguageCodes = { "vi", "en", "ko" };
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    public string[] LanguageDisplayNames { get; } = { "Tiếng Việt", "English", "한국어" };

    public SettingsViewModel(
        StorageService storageService,
        LocalPOIDatabase localDb,
        ILocationService locationService,
        IOfflineContentService offlineContentService)
    {
        _storageService = storageService;
        _localDb = localDb;
        _locationService = locationService;
        _offlineContentService = offlineContentService;
        LoadSettings();

        // Sync khi ngôn ngữ được đổi từ trang khác (ví dụ MainMapPage)
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) =>
        {
            _selectedLanguage = LocalizationResourceManager.Instance.CurrentLanguage;
            OnPropertyChanged(nameof(SelectedLanguageDisplayIndex));
            _ = RefreshOfflineStatusAsync();
        };

        _ = RefreshOfflineStatusAsync();
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

    partial void OnNotificationsEnabledChanged(bool value)
        => Preferences.Set("NotificationsEnabled", value);

    partial void OnAutoPlayAudioChanged(bool value)
        => Preferences.Set("AutoPlayAudio", value);

    /// <summary>Index trong LanguageDisplayNames để bind Picker.SelectedIndex</summary>
    public int SelectedLanguageDisplayIndex
    {
        get => Array.IndexOf(LanguageCodes, LocalizationResourceManager.Instance.CurrentLanguage);
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
    void SaveLanguage()
    {
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
            OfflinePackageStatus = L["SettingsOfflineDownloading"];

            var result = await _offlineContentService.DownloadOfflinePackageAsync(
                SelectedLanguage,
                IncludeAudioFilesInOfflinePackage);

            OfflinePackageStatus = result.Message;
            await Application.Current!.MainPage!.DisplayAlert(
                result.Success ? L["SettingsOfflineTitle"] : L["Error"],
                result.Message,
                L["OK"]);
        }
        finally
        {
            IsDownloadingOfflinePackage = false;
            await RefreshOfflineStatusAsync();
        }
    }

    [RelayCommand]
    async Task RefreshOfflineStatusAsync()
    {
        try
        {
            var status = await _offlineContentService.GetStatusAsync();
            var last = status.LastSyncUtc?.ToLocalTime().ToString("dd/MM HH:mm") ?? L["SettingsOfflineNeverSynced"];

            OfflinePackageStatus = string.Format(
                CultureInfo.CurrentCulture,
                L["SettingsOfflineStatusSummaryFormat"],
                status.PoiCount,
                status.ScriptCount,
                last);
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
}
