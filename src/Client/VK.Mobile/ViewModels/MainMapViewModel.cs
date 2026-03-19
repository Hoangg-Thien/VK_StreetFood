using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using VK.Mobile.Models;
using VK.Mobile.Services;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class MainMapViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ILocationService _locationService;
    private readonly IGeofenceEngine _geofenceEngine;
    private readonly INarrationCoordinator _narrationCoordinator;
    private readonly IOfflineContentService _offlineContentService;
    private readonly StorageService _storageService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILogger<MainMapViewModel> _logger;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    private DateTime _lastServerLocationUpdate = DateTime.MinValue;

    [ObservableProperty]
    private ObservableCollection<POIModel> _pois = new();

    public bool HasPOIs => Pois.Count > 0;

    partial void OnPoisChanged(ObservableCollection<POIModel> value)
    {
        OnPropertyChanged(nameof(HasPOIs));
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPOIs));
    }

    [ObservableProperty]
    private ObservableCollection<POIModel> _nearbyPOIs = new();

    [ObservableProperty]
    private POIModel? _nearestPoi;

    [ObservableProperty]
    private Location? _currentLocation;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isTracking;

    [ObservableProperty]
    private string? _poiLoadError;

    public bool HasPoiError => !string.IsNullOrEmpty(PoiLoadError);

    partial void OnPoiLoadErrorChanged(string? value)
        => OnPropertyChanged(nameof(HasPoiError));

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
        IGeofenceEngine geofenceEngine,
        INarrationCoordinator narrationCoordinator,
        IOfflineContentService offlineContentService,
        StorageService storageService,
        LocalPOIDatabase localDb,
        ILogger<MainMapViewModel> logger)
    {
        _apiService = apiService;
        _locationService = locationService;
        _geofenceEngine = geofenceEngine;
        _narrationCoordinator = narrationCoordinator;
        _offlineContentService = offlineContentService;
        _storageService = storageService;
        _localDb = localDb;
        _logger = logger;

        _locationService.LocationChanged += OnLocationChanged;

        // Sync SelectedLanguageIndex khi ngôn ngữ đổi từ trang khác (SettingsPage)
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) =>
        {
            _selectedLanguage = LocalizationResourceManager.Instance.CurrentLanguage;
            OnPropertyChanged(nameof(SelectedLanguageIndex));
        };
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

            // Get current location (with fallback to Vĩnh Khánh default)
            try
            {
                CurrentLocation = await _locationService.GetCurrentLocationAsync();
                if (CurrentLocation == null)
                {
                    _logger.LogWarning("Could not get current location, using Vĩnh Khánh default");
                    CurrentLocation = new Location(AppSettings.DefaultLatitude, AppSettings.DefaultLongitude);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get location, using Vĩnh Khánh default");
                CurrentLocation = new Location(AppSettings.DefaultLatitude, AppSettings.DefaultLongitude);
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

            // Nếu đang online, đồng bộ cache offline nền (không block UI)
            _ = _offlineContentService.AutoSyncWhenOnlineAsync(SelectedLanguage);

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
            PoiLoadError = null;
            List<POIModel> poiList = new();

            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                poiList = await _localDb.GetCachedPOIsAsync();
                _logger.LogInformation("Offline mode: loaded {Count} POIs from SQLite cache", poiList.Count);
            }
            else
            {
                poiList = await _apiService.GetAllPOIsAsync();
                _logger.LogInformation("API returned {Count} POIs", poiList.Count);

                if (poiList.Count > 0)
                {
                    await _localDb.SavePOIsAsync(poiList);
                }
                else
                {
                    _logger.LogWarning("API returned empty POI list, trying SQLite cache fallback");
                    poiList = await _localDb.GetCachedPOIsAsync();
                }
            }

            if (poiList.Count == 0)
            {
                PoiLoadError = L["MainMapNoOfflineData"];
            }

            Pois.Clear();
            foreach (var poi in poiList)
            {
                Pois.Add(poi);
            }

            PoiLoadError = Pois.Count > 0 ? null : PoiLoadError;

            _logger.LogInformation("Loaded {Count} POIs", Pois.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary POI load failed, trying SQLite cache fallback");

            try
            {
                var cached = await _localDb.GetCachedPOIsAsync();
                Pois.Clear();
                foreach (var poi in cached)
                {
                    Pois.Add(poi);
                }

                PoiLoadError = Pois.Count > 0
                    ? null
                    : L["MainMapNoOfflineDataFallback"];
            }
            catch (Exception cacheEx)
            {
                _logger.LogError(cacheEx, "Error loading POIs from SQLite cache");
                PoiLoadError = string.Format(
                    CultureInfo.CurrentCulture,
                    L["MainMapLoadErrorFormat"],
                    ex.Message);
            }
        }
    }

    [RelayCommand]
    private async Task TestAudioAsync(POIModel poi)
    {
        try
        {
            _logger.LogInformation(
                "Opening narration player for POI {PoiId} ({Name}), language {Lang}",
                poi.Id,
                poi.Name,
                SelectedLanguage);

            await _narrationCoordinator.OpenNowPlayingForPoiAsync(
                poi,
                SelectedLanguage,
                NearbyPOIs.Count > 0 ? NearbyPOIs : Pois,
                autoCloseExistingPlayer: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening NowPlaying for POI {Name}", poi.Name);
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.DisplayAlert(
                    L["Error"],
                    string.Format(CultureInfo.CurrentCulture, L["MainMapOpenPlayerFailedFormat"], ex.Message),
                    L["MainMapClose"])
            );
        }
    }

    [RelayCommand]
    private async Task StartTrackingAsync()
    {
        try
        {
            await _locationService.StartTrackingAsync();
            IsTracking = true;
            _geofenceEngine.MarkTrackingStarted();
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
            await Shell.Current.GoToAsync($"poidetail?poiId={poi.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to POI detail for {Name}", poi.Name);
            await Shell.Current.DisplayAlert(
                L["Error"],
                string.Format(CultureInfo.CurrentCulture, L["MainMapOpenDetailFailedFormat"], ex.Message),
                L["OK"]);
        }
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync(string languageCode)
    {
        SelectedLanguage = languageCode;
        await _storageService.SetPreferredLanguageAsync(languageCode);

        // Cập nhật LocalizationResourceManager → XAML tự cập nhật
        LocalizationResourceManager.Instance.SetLanguage(languageCode);

        if (CurrentTourist != null && !string.IsNullOrEmpty(CurrentTourist.DeviceId))
        {
            // Update tourist language preference via API if needed
            CurrentTourist.PreferredLanguage = languageCode;
            await _apiService.RegisterTouristAsync(CurrentTourist.DeviceId, languageCode);
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
            // Ưu tiên LocalizationResourceManager (dùng Preferences, đáng tin hơn SecureStorage)
            var language = LocalizationResourceManager.Instance.CurrentLanguage
                           ?? await _storageService.GetPreferredLanguageAsync()
                           ?? "vi";

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

        // Update location trên server: rate-limit 30 giây/lần + fire-and-forget
        // để không block geofence processing
        if (CurrentTourist != null && (DateTime.UtcNow - _lastServerLocationUpdate).TotalSeconds >= 30)
        {
            _lastServerLocationUpdate = DateTime.UtcNow;
            _ = _apiService.UpdateLocationAsync(
                CurrentTourist.Id,
                e.Location.Latitude,
                e.Location.Longitude);
        }

        // Tính POI gần nhất từ toàn bộ danh sách
        if (Pois.Count > 0)
        {
            NearestPoi = Pois
                .Where(p => p.Latitude != 0 || p.Longitude != 0)
                .OrderBy(p => _locationService.CalculateDistance(
                    e.Location.Latitude, e.Location.Longitude,
                    p.Latitude, p.Longitude))
                .FirstOrDefault();
        }

        var radiusMeters = Preferences.Get("GeofenceRadius", AppSettings.GeofenceRadiusMeters);
        var geofenceSelection = _geofenceEngine.SelectCandidates(
            e.Location,
            e.NearbyPOIs,
            Pois,
            radiusMeters,
            _locationService.CalculateDistance);

        NearbyPOIs.Clear();
        foreach (var nearbyPoi in geofenceSelection.NearbyPois)
            NearbyPOIs.Add(nearbyPoi);

        var bestCandidate = geofenceSelection.BestCandidate;

        if (bestCandidate != null)
            await OnGeofenceTriggeredAsync(bestCandidate);

    }

    private async Task OnGeofenceTriggeredAsync(POIModel poi)
    {
        try
        {
            if (!_geofenceEngine.ShouldTrigger(poi))
                return;

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

            if (!Preferences.Get("AutoPlayAudio", true))
            {
                _logger.LogInformation("AutoPlayAudio disabled, skip narration for POI: {Name}", poi.Name);
                return;
            }

            await _narrationCoordinator.OpenNowPlayingForPoiAsync(
                poi,
                SelectedLanguage,
                NearbyPOIs.Count > 0 ? NearbyPOIs : Pois,
                autoCloseExistingPlayer: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling geofence trigger");
        }
    }
}
