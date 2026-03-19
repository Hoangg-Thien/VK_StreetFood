using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly ILogger<FavoritesViewModel> _logger;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    public FavoritesViewModel(IApiService apiService, StorageService storageService, ILogger<FavoritesViewModel> logger)
    {
        _apiService = apiService;
        _storageService = storageService;
        _logger = logger;
    }

    [ObservableProperty]
    private ObservableCollection<POIModel> _favorites = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty = true;

    [RelayCommand]
    public async Task LoadFavoritesAsync()
    {
        try
        {
            IsLoading = true;
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId == null)
            {
                _logger.LogWarning("No tourist found");
                return;
            }

            var favorites = await _apiService.GetFavoritesAsync(touristId.Value);
            Favorites = new ObservableCollection<POIModel>(favorites);
            IsEmpty = !Favorites.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading favorites");
            await Application.Current!.MainPage!.DisplayAlert(
                L["Error"],
                L["FavoritesLoadFailed"],
                L["OK"]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task NavigateToPOI(POIModel poi)
    {
        await Shell.Current.GoToAsync($"POIDetail?poiId={poi.Id}");
    }

    [RelayCommand]
    async Task RemoveFavorite(POIModel poi)
    {
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            if (touristId == null) return;

            await _apiService.RemoveFavoriteAsync(touristId.Value, poi.Id);
            Favorites.Remove(poi);
            IsEmpty = !Favorites.Any();

            await Application.Current!.MainPage!.DisplayAlert(
                L["Success"],
                L["FavoritesRemovedSuccess"],
                L["OK"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite");
            await Application.Current!.MainPage!.DisplayAlert(
                L["Error"],
                L["FavoritesRemoveFailed"],
                L["OK"]);
        }
    }
}
