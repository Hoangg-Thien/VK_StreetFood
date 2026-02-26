using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly ILogger<HistoryViewModel> _logger;

    public HistoryViewModel(IApiService apiService, StorageService storageService, ILogger<HistoryViewModel> logger)
    {
        _apiService = apiService;
        _storageService = storageService;
        _logger = logger;
    }

    [ObservableProperty]
    private ObservableCollection<VisitLogModel> _visits = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty = true;

    [RelayCommand]
    public async Task LoadHistoryAsync()
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

            var history = await _apiService.GetVisitHistoryAsync(tourist.Id);
            Visits = new ObservableCollection<VisitLogModel>(history.OrderByDescending(v => v.VisitedAt));
            IsEmpty = !Visits.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading history");
            await Application.Current!.MainPage!.DisplayAlert("Lỗi", "Không thể tải lịch sử", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task NavigateToPOI(VisitLogModel visit)
    {
        try
        {
            var poi = await _apiService.GetPOIDetailAsync(visit.PointOfInterestId);
            var parameters = new Dictionary<string, object>
            {
                { "POI", poi }
            };
            await Shell.Current.GoToAsync("POIDetail", parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to POI");
        }
    }
}
