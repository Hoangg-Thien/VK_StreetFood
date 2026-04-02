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
    private readonly ITourSessionService _tourSession;
    private readonly ILogger<TourViewModel> _logger;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    public TourViewModel(IApiService apiService, ITourSessionService tourSession, ILogger<TourViewModel> logger)
    {
        _apiService = apiService;
        _tourSession = tourSession;
        _logger = logger;
        _tourSession.ActiveTourChanged += OnTourSessionChanged;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ObservableCollection<TourModel> _upcomingTours = new();

    [ObservableProperty]
    private ObservableCollection<TourModel> _completedTours = new();

    public bool HasRunningTour => _tourSession.ActiveTour != null;

    public string RunningTourName => _tourSession.ActiveTour?.Name ?? string.Empty;

    private void OnTourSessionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasRunningTour));
        OnPropertyChanged(nameof(RunningTourName));
    }

    [RelayCommand]
    private async Task LoadToursAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            var language = LocalizationResourceManager.Instance.CurrentLanguage;

            var tours = await _apiService.GetToursAsync(language);
            var readyTours = tours.Where(IsReadyStatus).ToList();
            var completedTours = tours.Where(IsCompletedStatus).ToList();

            UpcomingTours = new ObservableCollection<TourModel>(readyTours);

            CompletedTours = new ObservableCollection<TourModel>(completedTours);
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

    [RelayCommand]
    private Task ExitTourAsync()
    {
        _tourSession.ClearActiveTour();
        return Task.CompletedTask;
    }

    private async Task OpenTourAsync(TourModel? tour)
    {
        if (tour == null)
            return;

        var language = LocalizationResourceManager.Instance.CurrentLanguage;
        var detail = await _apiService.GetTourByIdAsync(tour.Id, language) ?? tour;
        if (detail.Points.Count == 0)
            detail = tour;

        if (detail.Points.Count == 0 && detail.FirstPoiId is int singlePoiId && singlePoiId > 0)
        {
            detail.Points = new List<TourPointModel>
            {
                new() { PoiId = singlePoiId, Name = detail.Name }
            };
        }

        if (detail.Points.Count == 0)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                L["Error"],
                L["ToursNoPoiToOpen"],
                L["OK"]);
            return;
        }

        _tourSession.SetActiveTour(detail);
        await Shell.Current.GoToAsync("//MainMap");
    }

    private static bool IsReadyStatus(TourModel tour)
        => string.Equals(tour.Status, "ready", StringComparison.OrdinalIgnoreCase)
           || string.Equals(tour.Status, "active", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompletedStatus(TourModel tour)
        => string.Equals(tour.Status, "completed", StringComparison.OrdinalIgnoreCase)
           || string.Equals(tour.Status, "inactive", StringComparison.OrdinalIgnoreCase);
}