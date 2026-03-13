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

    [ObservableProperty]
    private bool _isUsingOfflineData;

    partial void OnSearchTextChanged(string value) => FilterPOIs(value);

    public MenuViewModel(
        IApiService apiService,
        LocalPOIDatabase localDb,
        ILogger<MenuViewModel> logger)
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

            var onlinePois = await _apiService.GetAllPOIsAsync();
            if (onlinePois.Count > 0)
            {
                _allPois = onlinePois;
                IsUsingOfflineData = false;
                await _localDb.SavePOIsAsync(onlinePois);
                FilterPOIs(SearchText);
                return;
            }

            var cachedPois = await _localDb.GetCachedPOIsAsync();
            if (cachedPois.Count > 0)
            {
                _allPois = cachedPois;
                IsUsingOfflineData = true;
                ErrorMessage = null;
                FilterPOIs(SearchText);
                return;
            }

            _allPois = new List<POIModel>();
            IsUsingOfflineData = false;
            ErrorMessage = Connectivity.NetworkAccess == NetworkAccess.Internet
                ? "API chưa có dữ liệu."
                : "Không có mạng và chưa tải gói offline.";

            FilterPOIs(SearchText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POIs for menu");

            var cachedPois = await _localDb.GetCachedPOIsAsync();
            if (cachedPois.Count > 0)
            {
                _allPois = cachedPois;
                IsUsingOfflineData = true;
                ErrorMessage = null;
                FilterPOIs(SearchText);
            }
            else
            {
                ErrorMessage = "Không tải được danh sách. Kiểm tra kết nối mạng hoặc tải gói offline.";
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
