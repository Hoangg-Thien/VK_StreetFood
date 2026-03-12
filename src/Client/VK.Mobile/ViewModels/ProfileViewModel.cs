using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly ILogger<ProfileViewModel> _logger;

    public ProfileViewModel(IApiService apiService, StorageService storageService, ILogger<ProfileViewModel> logger)
    {
        _apiService = apiService;
        _storageService = storageService;
        _logger = logger;
    }

    [ObservableProperty]
    private TouristModel? _tourist;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalVisits;

    [ObservableProperty]
    private int _totalFavorites;

    [ObservableProperty]
    private string _preferredLanguage = "vi";

    [ObservableProperty]
    private string _memberSince = string.Empty;

    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        try
        {
            IsLoading = true;
            var touristId = await _storageService.GetTouristIdAsync();

            if (touristId != null)
            {
                PreferredLanguage = await _storageService.GetPreferredLanguageAsync() ?? "vi";

                // Load favorites count
                var favorites = await _apiService.GetFavoritesAsync(touristId.Value);
                TotalFavorites = favorites.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task NavigateToSettings()
    {
        await Shell.Current.GoToAsync("///Settings");
    }

    [RelayCommand]
    async Task NavigateToFavorites()
    {
        await Shell.Current.GoToAsync("///Favorites");
    }

    public string GetLanguageDisplayName(string code)
    {
        return code switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "ja" => "日本語",
            "ko" => "한국어",
            "zh" => "中文",
            _ => code
        };
    }
}
