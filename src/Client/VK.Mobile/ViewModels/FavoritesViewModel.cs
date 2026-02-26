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
            var tourist = await _storageService.GetTouristAsync();
            if (tourist == null)
            {
                _logger.LogWarning("No tourist found");
                return;
            }

            var favorites = await _apiService.GetFavoritesAsync(tourist.Id);
            var pois = favorites
                .Where(f => f.Poi != null)
                .Select(f => f.Poi!)
                .ToList();
            Favorites = new ObservableCollection<POIModel>(pois);
            IsEmpty = !Favorites.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading favorites");
            await Application.Current!.MainPage!.DisplayAlert("Lỗi", "Không thể tải danh sách yêu thích", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task NavigateToPOI(POIModel poi)
    {
        var parameters = new Dictionary<string, object>
        {
            { "POI", poi }
        };
        await Shell.Current.GoToAsync("POIDetail", parameters);
    }

    [RelayCommand]
    async Task RemoveFavorite(POIModel poi)
    {
        try
        {
            var tourist = await _storageService.GetTouristAsync();
            if (tourist == null) return;

            await _apiService.RemoveFavoriteAsync(tourist.Id, poi.Id);
            Favorites.Remove(poi);
            IsEmpty = !Favorites.Any();

            await Application.Current!.MainPage!.DisplayAlert("Thành công", "Đã xóa khỏi danh sách yêu thích", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite");
            await Application.Current!.MainPage!.DisplayAlert("Lỗi", "Không thể xóa", "OK");
        }
    }
}
