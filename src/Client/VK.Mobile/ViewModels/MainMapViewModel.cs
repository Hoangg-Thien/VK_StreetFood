using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class MainMapViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ILocationService _locationService;
    private readonly IAudioService _audioService;
    private readonly ITTSService _ttsService;
    private readonly StorageService _storageService;
    private readonly ILogger<MainMapViewModel> _logger;

    // Debounce: bỏ qua các trigger ngay sau khi khởi động
    private DateTime _trackingStartTime = DateTime.MaxValue;
    // Cooldown: theo dõi lần cuối mỗi POI được trigger
    private readonly Dictionary<int, DateTime> _geofenceLastTriggered = new();

    [ObservableProperty]
    private ObservableCollection<POIModel> _pois = new();

    [ObservableProperty]
    private ObservableCollection<POIModel> _nearbyPOIs = new();

    [ObservableProperty]
    private Location? _currentLocation;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isTracking;

    [ObservableProperty]
    private string _selectedLanguage = "vi";

    /// <summary>Picker index tương ứng: 0=vi, 1=en, 2=ko</summary>
    public int SelectedLanguageIndex
    {
        get => _selectedLanguage switch { "en" => 1, "ko" => 2, _ => 0 };
        set
        {
            var code = value switch { 1 => "en", 2 => "ko", _ => "vi" };
            if (code != _selectedLanguage)
                _ = ChangeLanguageCommand.ExecuteAsync(code);
        }
    }

    partial void OnSelectedLanguageChanged(string value)
        => OnPropertyChanged(nameof(SelectedLanguageIndex));

    [ObservableProperty]
    private TouristModel? _currentTourist;

    public MainMapViewModel(
        IApiService apiService,
        ILocationService locationService,
        IAudioService audioService,
        ITTSService ttsService,
        StorageService storageService,
        ILogger<MainMapViewModel> logger)
    {
        _apiService = apiService;
        _locationService = locationService;
        _audioService = audioService;
        _ttsService = ttsService;
        _storageService = storageService;
        _logger = logger;

        _locationService.LocationChanged += OnLocationChanged;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;

            // Initialize tourist (don't fail if API is down)
            try
            {
                await InitializeTouristAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize tourist, continuing anyway");
            }

            // Get current location (with fallback to default)
            try
            {
                CurrentLocation = await _locationService.GetCurrentLocationAsync();
                if (CurrentLocation == null)
                {
                    _logger.LogWarning("Could not get current location, using default");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get location, using default");
            }

            // Load POIs (best effort)
            try
            {
                await LoadPOIsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load POIs");
            }

            // Start tracking (non-blocking)
            try
            {
                await StartTrackingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start tracking");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing");
            // Don't show error dialog, just log it
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadPOIsAsync()
    {
        try
        {
            List<POIModel> poiList = new();

            // Try to load POIs from API
            try
            {
                if (CurrentLocation != null)
                {
                    poiList = await _apiService.GetNearbyPOIsAsync(
                        CurrentLocation.Latitude,
                        CurrentLocation.Longitude,
                        5.0); // 5km radius
                }
                else
                {
                    poiList = await _apiService.GetAllPOIsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API not available, using empty POI list");
                // Continue with empty list
            }

            Pois.Clear();
            foreach (var poi in poiList)
            {
                Pois.Add(poi);
            }

            _logger.LogInformation("Loaded {Count} POIs", Pois.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POIs");
        }
    }

    [RelayCommand]
    private async Task StartTrackingAsync()
    {
        try
        {
            await _locationService.StartTrackingAsync();
            IsTracking = true;
            // Ghi nhớ thời điểm bắt đầu để debounce các trigger quá sớm
            _trackingStartTime = DateTime.UtcNow;
            _logger.LogInformation("Location tracking started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting tracking");
        }
    }

    [RelayCommand]
    private async Task StopTrackingAsync()
    {
        await _locationService.StopTrackingAsync();
        IsTracking = false;
    }

    [RelayCommand]
    private async Task POISelectedAsync(POIModel poi)
    {
        try
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "POI", poi }
            };

            await Shell.Current.GoToAsync("poidetail", navigationParameter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to POI detail");
        }
    }

    [RelayCommand]
    private async Task OpenQRScannerAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("qrscan");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening QR scanner");
        }
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync(string languageCode)
    {
        SelectedLanguage = languageCode;
        await _storageService.SetPreferredLanguageAsync(languageCode);

        // Cập nhật LocalizationResourceManager → XAML tự cập nhật
        LocalizationResourceManager.Instance.SetLanguage(languageCode);

        if (CurrentTourist != null)
        {
            // Update tourist language preference via API if needed
        }

        // Reload POIs with new language
        await LoadPOIsAsync();
    }

    private async Task InitializeTouristAsync()
    {
        try
        {
            var touristId = await _storageService.GetTouristIdAsync();
            var deviceId = await _storageService.GetDeviceIdAsync();
            var language = await _storageService.GetPreferredLanguageAsync() ?? "vi";

            SelectedLanguage = language;

            if (touristId == null || string.IsNullOrEmpty(deviceId))
            {
                // Generate new device ID
                deviceId = Guid.NewGuid().ToString();

                Location? location = null;
                try
                {
                    location = await _locationService.GetCurrentLocationAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get location for tourist registration");
                }

                try
                {
                    var tourist = await _apiService.RegisterTouristAsync(
                        deviceId,
                        language,
                        location?.Latitude,
                        location?.Longitude);

                    if (tourist != null)
                    {
                        await _storageService.SetTouristIdAsync(tourist.Id);
                        await _storageService.SetDeviceIdAsync(deviceId);
                        CurrentTourist = tourist;
                        _logger.LogInformation("New tourist registered: {Id}", tourist.Id);
                    }
                    else
                    {
                        _logger.LogWarning("API returned null for tourist registration");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not register tourist via API (offline mode)");
                    // Continue without tourist registration
                }
            }
            else
            {
                CurrentTourist = new TouristModel
                {
                    Id = touristId.Value,
                    DeviceId = deviceId,
                    PreferredLanguage = language
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing tourist");
            throw;
        }
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        CurrentLocation = e.Location;

        // Lưu vị trí ẩn danh để vẽ heatmap (không gắn với tourist ID)
        _storageService.AppendLocation(e.Location.Latitude, e.Location.Longitude);

        // Update location on server
        if (CurrentTourist != null)
        {
            await _apiService.UpdateLocationAsync(
                CurrentTourist.Id,
                e.Location.Latitude,
                e.Location.Longitude);
        }

        // Update nearby POIs
        NearbyPOIs.Clear();

        // Sắp xếp POI theo Priority giảm dần trước khi xử lý geofence
        var sortedPOIs = e.NearbyPOIs.OrderByDescending(p => p.Priority).ToList();

        foreach (var poi in sortedPOIs)
        {
            NearbyPOIs.Add(poi);

            // Check if within geofence radius
            var distance = _locationService.CalculateDistance(
                e.Location.Latitude,
                e.Location.Longitude,
                poi.Latitude,
                poi.Longitude) * 1000; // to meters

            if (distance <= AppSettings.GeofenceRadiusMeters)
            {
                await OnGeofenceTriggeredAsync(poi);
            }
        }
    }

    private async Task OnGeofenceTriggeredAsync(POIModel poi)
    {
        try
        {
            var now = DateTime.UtcNow;

            // --- Debounce: bỏ qua trigger trong vài giây đầu sau khi bắt đầu tracking ---
            if ((now - _trackingStartTime).TotalMilliseconds < AppSettings.GeofenceDebounceMs)
            {
                _logger.LogDebug("Geofence debounced for POI {Id} (too soon after start)", poi.Id);
                return;
            }

            // --- Cooldown: mỗi POI chỉ trigger lại sau X phút ---
            if (_geofenceLastTriggered.TryGetValue(poi.Id, out var lastTrigger))
            {
                var cooldownEnd = lastTrigger.AddMinutes(AppSettings.GeofenceCooldownMinutes);
                if (now < cooldownEnd)
                {
                    _logger.LogDebug("Geofence cooldown active for POI {Name}, next trigger in {Remaining:F0}s",
                        poi.Name, (cooldownEnd - now).TotalSeconds);
                    return;
                }
            }

            // Ghi nhớ thời điểm trigger để cooldown lần sau
            _geofenceLastTriggered[poi.Id] = now;

            _logger.LogInformation("Geofence triggered for POI: {Name}", poi.Name);

            // Log visit
            if (CurrentTourist != null && CurrentLocation != null)
            {
                await _apiService.LogVisitAsync(
                    CurrentTourist.Id,
                    poi.Id,
                    "geofence",
                    CurrentLocation.Latitude,
                    CurrentLocation.Longitude);

                // Track analytics event
                await _apiService.TrackEventAsync(
                    CurrentTourist.Id,
                    poi.Id,
                    "geofence_enter",
                    SelectedLanguage);
            }

            // Show notification
            await Shell.Current.DisplayAlert(
                "Point of Interest",
                $"You are near: {poi.Name}",
                "OK");

            // Phát thuyết minh qua TTSService (pre-recorded → Google TTS → MAUI TTS)
            await _ttsService.SpeakPOIAsync(poi, SelectedLanguage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling geofence trigger");
        }
    }
}
