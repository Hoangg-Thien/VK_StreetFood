using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class TourViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ILogger<TourViewModel> _logger;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    public TourViewModel(IApiService apiService, ILogger<TourViewModel> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private TourModel? _activeTour;

    [ObservableProperty]
    private ObservableCollection<TourModel> _upcomingTours = new();

    [ObservableProperty]
    private ObservableCollection<TourModel> _completedTours = new();

    public bool HasActiveTour => ActiveTour != null;

    partial void OnActiveTourChanged(TourModel? value)
        => OnPropertyChanged(nameof(HasActiveTour));

    [RelayCommand]
    private async Task LoadToursAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var tours = await _apiService.GetToursAsync();

            ActiveTour = tours.FirstOrDefault(t => string.Equals(t.Status, "active", StringComparison.OrdinalIgnoreCase));

            UpcomingTours = new ObservableCollection<TourModel>(
                tours.Where(t => string.Equals(t.Status, "draft", StringComparison.OrdinalIgnoreCase)));

            CompletedTours = new ObservableCollection<TourModel>(
                tours.Where(t => string.Equals(t.Status, "inactive", StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tours");
            ErrorMessage = L["ToursLoadFailed"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResumeTourAsync(TourModel? tour)
    {
        await OpenTourAsync(tour);
    }

    [RelayCommand]
    private async Task StartTourAsync(TourModel? tour)
    {
        await OpenTourAsync(tour);
    }

    [RelayCommand]
    private async Task OpenSummaryAsync(TourModel? tour)
    {
        await OpenTourAsync(tour);
    }

    private async Task OpenTourAsync(TourModel? tour)
    {
        if (tour == null)
            return;

        var targetPoiId = tour.FirstPoiId;
        if (targetPoiId == null)
        {
            var detail = await _apiService.GetTourByIdAsync(tour.Id);
            targetPoiId = detail?.FirstPoiId;
        }

        if (targetPoiId == null)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                L["Error"],
                L["ToursNoPoiToOpen"],
                L["OK"]);
            return;
        }

        await Shell.Current.GoToAsync($"POIDetail?poiId={targetPoiId.Value}");
    }
}