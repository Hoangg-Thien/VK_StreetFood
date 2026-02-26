using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StorageService _storageService;

    public SettingsViewModel(StorageService storageService)
    {
        _storageService = storageService;
        LoadSettings();
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

    public string[] AvailableLanguages { get; } = AppSettings.SupportedLanguages;

    public Dictionary<string, string> LanguageNames => AppSettings.LanguageNames;

    void LoadSettings()
    {
        SelectedLanguage = Preferences.Get("Language", "vi");
        NotificationsEnabled = Preferences.Get("NotificationsEnabled", true);
        AutoPlayAudio = Preferences.Get("AutoPlayAudio", true);
        GeofenceRadius = Preferences.Get("GeofenceRadius", AppSettings.GeofenceRadiusMeters);
        LocationUpdateInterval = Preferences.Get("LocationUpdateInterval", AppSettings.LocationUpdateIntervalSeconds);
    }

    [RelayCommand]
    void SaveLanguage()
    {
        Preferences.Set("Language", SelectedLanguage);
        // TODO: Notify app of language change
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
            "Xóa bộ nhớ đệm",
            "Bạn có chắc muốn xóa toàn bộ bộ nhớ đệm?",
            "Xóa", "Hủy");

        if (confirm)
        {
            // TODO: Clear cache
            await Application.Current.MainPage.DisplayAlert("Thành công", "Đã xóa bộ nhớ đệm", "OK");
        }
    }

    [RelayCommand]
    async Task Logout()
    {
        bool confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Đăng xuất",
            "Bạn có chắc muốn đăng xuất?",
            "Đăng xuất", "Hủy");

        if (confirm)
        {
            await _storageService.ClearAsync();
            await Shell.Current.GoToAsync("///Welcome");
        }
    }
}
