using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using VK.Mobile.Models;
using VK.Mobile.Services;
using VK.Mobile.Views;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class MainMapViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ILocationService _locationService;
    private readonly ITTSService _ttsService;
    private readonly IOfflineContentService _offlineContentService;
    private readonly StorageService _storageService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILogger<MainMapViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Debounce: bỏ qua các trigger ngay sau khi khởi động
    private DateTime _trackingStartTime = DateTime.MaxValue;
    // Cooldown: theo dõi lần cuối mỗi POI được trigger
    private readonly Dictionary<int, DateTime> _geofenceLastTriggered = new();

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

    /// <summary>Fires on MainThread khi geofence trigger – MainMapPage sẽ tự mở NowPlayingPage.</summary>
    public event EventHandler<POIModel>? GeofencePOITriggered;

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
        ITTSService ttsService,
        IOfflineContentService offlineContentService,
        StorageService storageService,
        LocalPOIDatabase localDb,
        ILogger<MainMapViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _apiService = apiService;
        _locationService = locationService;
        _ttsService = ttsService;
        _offlineContentService = offlineContentService;
        _storageService = storageService;
        _localDb = localDb;
        _logger = logger;
        _serviceProvider = serviceProvider;

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

            // Load từ API; nếu lỗi thì dùng SQLite cache (offline)
            try
            {
                poiList = await _apiService.GetAllPOIsAsync();
                _logger.LogInformation("API returned {Count} POIs", poiList.Count);

                // Lưu vào SQLite cache để dùng khi offline
                if (poiList.Count > 0)
                    await _localDb.SavePOIsAsync(poiList);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API not available, trying SQLite cache");

                // Fallback: đọc từ SQLite cache
                poiList = await _localDb.GetCachedPOIsAsync();
                if (poiList.Count > 0)
                {
                    _logger.LogInformation("Loaded {Count} POIs from SQLite cache (offline)", poiList.Count);
                    PoiLoadError = null; // cache hoạt động OK
                }
                else
                {
                    PoiLoadError = $"Không thể kết nối API và không có dữ liệu offline: {ex.Message}";
                }
            }

            // Nếu API trả về rỗng (không có lỗi)
            if (poiList.Count == 0 && PoiLoadError == null)
            {
                _logger.LogWarning("No POIs returned from API. Check API connection and database.");
                PoiLoadError = "API không trả về POIs. Hãy kiểm tra server đang chạy tại cổng 5089.";
            }

            Pois.Clear();
            foreach (var poi in poiList)
            {
                Pois.Add(poi);
            }

            if (Pois.Count > 0)
                PoiLoadError = null; // Clear error khi load thành công

            _logger.LogInformation("Loaded {Count} POIs from API", Pois.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading POIs");
            PoiLoadError = $"Lỗi load POIs: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestAudioAsync(POIModel poi)
    {
        try
        {
            _logger.LogInformation("Opening NowPlaying for POI: {Name}, Language: {Lang}", poi.Name, SelectedLanguage);

            // Dừng audio đang chạy
            await _ttsService.StopAsync();

            // Fetch nội dung audio đúng ngôn ngữ từ API
            string audioText;
            try
            {
                var audioContent = await _apiService.GetAudioForPOIAsync(poi.Id, SelectedLanguage);
                if (audioContent != null && !string.IsNullOrWhiteSpace(audioContent.TextContent))
                {
                    audioText = audioContent.TextContent.Length > 500
                        ? audioContent.TextContent[..500]
                        : audioContent.TextContent;
                    await _offlineContentService.CacheNarrationScriptAsync(
                        poi.Id,
                        audioContent.LanguageCode,
                        audioContent.TextContent,
                        audioContent.AudioFileUrl,
                        audioContent.DurationInSeconds);
                }
                else
                {
                    var cached = await _offlineContentService.GetCachedNarrationTextAsync(poi.Id, SelectedLanguage);
                    audioText = !string.IsNullOrWhiteSpace(cached)
                        ? cached
                        : BuildFallbackText(poi);
                }
            }
            catch
            {
                var cached = await _offlineContentService.GetCachedNarrationTextAsync(poi.Id, SelectedLanguage);
                audioText = !string.IsNullOrWhiteSpace(cached)
                    ? cached
                    : BuildFallbackText(poi);
            }

            // Mở NowPlayingPage dạng modal overlay
            var page = _serviceProvider.GetRequiredService<NowPlayingPage>();
            var vm = (NowPlayingViewModel)page.BindingContext;

            vm.SetAllPois(NearbyPOIs.Count > 0 ? NearbyPOIs : Pois);

            static string FormatDist(double? km) => km switch
            {
                null or 0 => "",
                < 0.1 => $"{(km.Value * 1000):F0}m away",
                _ => $"{km.Value:F1} km away"
            };

            vm.Initialize(
                poi.Id, poi.Name ?? string.Empty, poi.CategoryName ?? string.Empty,
                poi.ImageUrl ?? string.Empty, audioText, SelectedLanguage,
                poi.Address ?? string.Empty, FormatDist(poi.DistanceKm));
            await Shell.Current.Navigation.PushModalAsync(page, animated: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening NowPlaying for POI {Name}", poi.Name);
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.DisplayAlert("⚠️ Lỗi", $"Không mở được trình phát: {ex.Message}", "Đóng")
            );
        }
    }

    private string BuildFallbackText(POIModel poi) => SelectedLanguage switch
    {
        "en" => $"{poi.Name}. {(string.IsNullOrWhiteSpace(poi.Description) ? "A famous street food spot in Vinh Khanh." : poi.Description[..Math.Min(300, poi.Description.Length)])}",
        "ko" => $"{poi.Name}. 이 곳은 빈칸의 유명한 길거리 음식 명소입니다.",
        _ => $"{poi.Name}. {(string.IsNullOrWhiteSpace(poi.Description) ? "Điểm ẩm thực nổi tiếng tại Vĩnh Khánh." : poi.Description[..Math.Min(300, poi.Description.Length)])}"
    };

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
            await Shell.Current.GoToAsync($"poidetail?poiId={poi.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to POI detail for {Name}", poi.Name);
            await Shell.Current.DisplayAlert("Lỗi", $"Không mở được chi tiết: {ex.Message}", "OK");
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

        // Sắp xếp POI theo Priority giảm dần trước khi xử lý geofence
        var sortedPOIs = e.NearbyPOIs.OrderByDescending(p => p.Priority).ToList();
        var radiusMeters = Preferences.Get("GeofenceRadius", AppSettings.GeofenceRadiusMeters);
        var geofenceCandidates = new List<(POIModel Poi, double DistanceMeters)>();

        foreach (var poi in sortedPOIs)
        {
            NearbyPOIs.Add(poi);

            // Check if within geofence radius
            var distance = _locationService.CalculateDistance(
                e.Location.Latitude,
                e.Location.Longitude,
                poi.Latitude,
                poi.Longitude) * 1000; // to meters

            poi.DistanceKm = distance / 1000.0;

            if (distance <= radiusMeters)
                geofenceCandidates.Add((poi, distance));
        }

        var bestCandidate = geofenceCandidates
            .OrderByDescending(c => c.Poi.Priority)
            .ThenBy(c => c.DistanceMeters)
            .Select(c => c.Poi)
            .FirstOrDefault();

        if (bestCandidate != null)
            await OnGeofenceTriggeredAsync(bestCandidate);
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

            if (!Preferences.Get("AutoPlayAudio", true))
            {
                _logger.LogInformation("AutoPlayAudio disabled, skip narration for POI: {Name}", poi.Name);
                return;
            }

            // Phát thuyết minh tự động: đóng NowPlayingPage cũ → mở cái mới
            await MainThread.InvokeOnMainThreadAsync(() =>
                GeofencePOITriggered?.Invoke(this, poi)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling geofence trigger");
        }
    }
}
