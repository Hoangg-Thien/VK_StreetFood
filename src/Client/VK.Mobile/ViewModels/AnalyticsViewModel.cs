using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly ILogger<AnalyticsViewModel> _logger;

    public AnalyticsViewModel(
        IApiService apiService,
        StorageService storageService,
        ILogger<AnalyticsViewModel> logger)
    {
        _apiService = apiService;
        _storageService = storageService;
        _logger = logger;
    }

    // ---- Cá nhân ----
    [ObservableProperty] private int _totalVisits;
    [ObservableProperty] private int _totalAudioPlays;
    [ObservableProperty] private int _totalQRScans;
    [ObservableProperty] private int _totalGeofenceEnters;
    [ObservableProperty] private string _totalAudioTime = "0 phút";
    [ObservableProperty] private string _mostVisitedPOI = "-";
    [ObservableProperty] private string _favoriteLanguage = "-";

    // ---- Top POIs ----
    [ObservableProperty]
    private ObservableCollection<TopPOIModel> _topPOIs = new();

    // ---- State ----
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;

            var tourist = await _storageService.GetTouristAsync();

            // Load personal stats and top POIs in parallel
            Task<TouristStatsModel?> statsTask;
            if (tourist != null)
                statsTask = _apiService.GetMyStatsAsync(tourist.Id);
            else
                statsTask = Task.FromResult<TouristStatsModel?>(null);

            var (stats, topPOIs) = await (statsTask, _apiService.GetTopPOIsAsync(10))
                .WhenAll();

            // Apply personal stats
            if (stats != null)
            {
                TotalVisits = stats.TotalVisits;
                TotalAudioPlays = stats.TotalAudioPlays;
                TotalQRScans = stats.TotalQRScans;
                TotalGeofenceEnters = stats.TotalGeofenceEnters;
                TotalAudioTime = stats.TotalAudioMinutes < 60
                    ? $"{stats.TotalAudioMinutes:F0} phút"
                    : $"{stats.TotalAudioMinutes / 60:F1} giờ";
                MostVisitedPOI = string.IsNullOrEmpty(stats.MostVisitedPOI) ? "Chưa có" : stats.MostVisitedPOI;
                FavoriteLanguage = stats.FavoriteLanguage switch
                {
                    "vi" => "Tiếng Việt",
                    "en" => "English",
                    "ko" => "한국어",
                    _ => stats.FavoriteLanguage
                };
            }

            // Apply top POIs
            TopPOIs.Clear();
            foreach (var poi in topPOIs)
                TopPOIs.Add(poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics");
            HasError = true;
            ErrorMessage = "Không thể tải dữ liệu. Vui lòng thử lại.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task RetryAsync() => await LoadAsync();
}

// Helper extension to await two tasks as a tuple
file static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenAll<T1, T2>(this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }
}
