using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StorageService _storageService;
    private static readonly string[] LanguageCodes = { "vi", "en", "ko" };

    public string[] LanguageDisplayNames { get; } = { "Tiếng Việt", "English", "한국어" };

    public SettingsViewModel(StorageService storageService)
    {
        _storageService = storageService;
        LoadSettings();

        // Sync khi ngôn ngữ được đổi từ trang khác (ví dụ MainMapPage)
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) =>
        {
            _selectedLanguage = LocalizationResourceManager.Instance.CurrentLanguage;
            OnPropertyChanged(nameof(SelectedLanguageDisplayIndex));
        };
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
        Preferences.Set("GeofenceRadius", GeofenceRadius);
    }

    [RelayCommand]
    void SaveLocationInterval()
    {
        Preferences.Set("LocationUpdateInterval", LocationUpdateInterval);
    }

    [RelayCommand]
    async Task ClearCache()
    {
        bool confirm = await Application.Current!.MainPage!.DisplayAlert(
            LocalizationResourceManager.Instance["SettingsClearCache"],
            "Bạn có chắc muốn xóa toàn bộ bộ nhớ đệm?",
            LocalizationResourceManager.Instance["OK"],
            LocalizationResourceManager.Instance["Cancel"]);

        if (confirm)
        {
            await Application.Current.MainPage.DisplayAlert(
                LocalizationResourceManager.Instance["OK"],
                "Đã xóa bộ nhớ đệm",
                LocalizationResourceManager.Instance["OK"]);
        }
    }

    [RelayCommand]
    async Task Logout()
    {
        bool confirm = await Application.Current!.MainPage!.DisplayAlert(
            LocalizationResourceManager.Instance["SettingsLogout"],
            "Bạn có chắc muốn đăng xuất?",
            LocalizationResourceManager.Instance["SettingsLogout"],
            LocalizationResourceManager.Instance["Cancel"]);

        if (confirm)
        {
            await _storageService.ClearAsync();
            await Shell.Current.GoToAsync("///Welcome");
        }
    }
}
