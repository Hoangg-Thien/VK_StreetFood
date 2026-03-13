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

    public MenuViewModel(IApiService apiService, ILogger<MenuViewModel> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadPOIsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            _allPois = await _apiService.GetAllPOIsAsync();
            FilterPOIs(SearchText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POIs for menu");
            ErrorMessage = "Không tải được danh sách. Kiểm tra kết nối mạng.";
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
