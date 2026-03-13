using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class MenuViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILogger<MenuViewModel> _logger;

    private List<POIModel> _allPois = new();

    [ObservableProperty]
    private ObservableCollection<POIModel> _filteredPois = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    partial void OnSearchTextChanged(string value) => FilterPOIs(value);

    public MenuViewModel(IApiService apiService, LocalPOIDatabase localDb, ILogger<MenuViewModel> logger)
    {
        _apiService = apiService;
        _localDb = localDb;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadPOIsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            List<POIModel> poiList;
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                poiList = await _localDb.GetCachedPOIsAsync();
                _logger.LogInformation("Offline mode: loaded {Count} POIs from SQLite cache", poiList.Count);
            }
            else
            {
                poiList = await _apiService.GetAllPOIsAsync();
                if (poiList.Count > 0)
                {
                    await _localDb.SavePOIsAsync(poiList);
                }
                else
                {
                    _logger.LogWarning("API returned empty POI list for menu, trying SQLite cache fallback");
                    poiList = await _localDb.GetCachedPOIsAsync();
                }
            }

            _allPois = poiList;
            FilterPOIs(SearchText);

            if (_allPois.Count == 0)
                ErrorMessage = "Không có dữ liệu POI offline. Hãy bật mạng để đồng bộ.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary menu POI load failed, trying SQLite cache fallback");

            try
            {
                _allPois = await _localDb.GetCachedPOIsAsync();
                FilterPOIs(SearchText);
                ErrorMessage = _allPois.Count > 0
                    ? null
                    : "Không tải được danh sách. Kiểm tra kết nối mạng.";
            }
            catch (Exception cacheEx)
            {
                _logger.LogError(cacheEx, "Error loading menu POIs from SQLite cache");
                ErrorMessage = "Không tải được danh sách. Kiểm tra kết nối mạng.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void FilterPOIs(string? query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allPois
            : _allPois.Where(p =>
                (p.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Address?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
              .ToList();

        FilteredPois = new ObservableCollection<POIModel>(filtered);
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private async Task SelectPOIAsync(POIModel poi)
    {
        await Shell.Current.GoToAsync($"POIDetail?poiId={poi.Id}");
    }
}
