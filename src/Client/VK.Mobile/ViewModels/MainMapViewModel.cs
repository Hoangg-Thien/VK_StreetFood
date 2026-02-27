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

    public bool HasPOIs => Pois.Count > 0;

    partial void OnPoisChanged(ObservableCollection<POIModel> value)
    {
        OnPropertyChanged(nameof(HasPOIs));
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPOIs));
    }

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
                _logger.LogWarning(ex, "API not available, loading sample POIs for testing");
            }

            // Fallback: load sample POIs if API returned nothing
            if (poiList.Count == 0)
            {
                poiList = GetSamplePOIs();
                _logger.LogInformation("Using {Count} sample POIs (offline mode)", poiList.Count);
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

    /// <summary>
    /// Sample POIs from Vĩnh Khánh Food Street for offline testing.
    /// </summary>
    private static List<POIModel> GetSamplePOIs() => new()
    {
        new POIModel
        {
            Id = 1, Name = "Cổng chào Phố Ẩm thực Vĩnh Khánh",
            Description = "Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – 'thiên đường không ngủ' của Quận 4. Được Time Out vinh danh là một trong những đường phố thú vị nhất thế giới năm 2025.",
            Latitude = 10.761905898335831, Longitude = 106.70222716527056,
            Address = "Vĩnh Khánh, Phường 9, Quận 4, TP.HCM", QrCode = "VK-ENTRANCE",
            CategoryName = "Đặc sản", Priority = 10
        },
        new POIModel
        {
            Id = 2, Name = "Ốc Vũ",
            Description = "Quán ốc nổi tiếng với hơn một thập kỷ hoạt động. Nổi tiếng với nguồn hải sản tươi sống và nước sốt me 'thần thánh' - chua thanh, cay nhẹ, tạo nên bản giao hưởng vị giác khó quên.",
            Latitude = 10.761518431027818, Longitude = 106.70271542519974,
            Address = "37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM", QrCode = "VK-OC-VU",
            CategoryName = "Ốc & Hải sản", AverageRating = 4.5, Priority = 5
        },
        new POIModel
        {
            Id = 3, Name = "Ốc Thảo",
            Description = "Không gian rộng rãi, thoáng đãng với triết lý tôn vinh vị ngọt tự nhiên của nguyên liệu. Ốc len xào dừa được đánh giá là cực phẩm với nước cốt dừa béo ngậy không gây ngán.",
            Latitude = 10.761795162597451, Longitude = 106.70239298897182,
            Address = "383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM", QrCode = "VK-OC-THAO",
            CategoryName = "Ốc & Hải sản", AverageRating = 4.3, Priority = 4
        },
        new POIModel
        {
            Id = 4, Name = "Ốc Sáu Nở",
            Description = "Quán ốc gạo cội từ thập niên 90 với kỹ thuật nướng mỡ hành gia truyền. Sò điệp nướng mỡ hành đậu phộng với lửa than hồng là 'signature dish'.",
            Latitude = 10.762087, Longitude = 106.70261,
            Address = "412 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM", QrCode = "VK-OC-SAU-NO",
            CategoryName = "Ốc & Hải sản", AverageRating = 4.6, Priority = 5
        },
        new POIModel
        {
            Id = 5, Name = "Lẩu Dê Phước Thịnh",
            Description = "Thương hiệu lẩu dê lâu đời nhất phố Vĩnh Khánh. Nồi lẩu dê nấu tiêu xanh hoặc thuốc bắc với nước dùng ninh xương 6 tiếng.",
            Latitude = 10.762328, Longitude = 106.70305,
            Address = "Vĩnh Khánh, Phường 9, Quận 4, TP.HCM", QrCode = "VK-LAU-DE",
            CategoryName = "Lẩu & Nướng", AverageRating = 4.4, Priority = 3
        },
        new POIModel
        {
            Id = 6, Name = "Cơm tấm 168",
            Description = "Cơm tấm đêm nổi tiếng với sườn nướng than hồng thơm lừng, bì giòn và chả trứng. Mở cửa từ 5h chiều đến 3h sáng.",
            Latitude = 10.760896, Longitude = 106.70195,
            Address = "168 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM", QrCode = "VK-COM-TAM",
            CategoryName = "Món chính", AverageRating = 4.2, Priority = 2
        },
    };

    [RelayCommand]
    private async Task TestAudioAsync(POIModel poi)
    {
        try
        {
            _logger.LogInformation("Testing audio for POI: {Name}", poi.Name);

            await Shell.Current.DisplayAlert(
                "🔊 Đang phát thuyết minh",
                poi.Name,
                "OK");

            // Use TTS fallback (MAUI TextToSpeech since API is offline)
            await _ttsService.SpeakPOIAsync(poi, SelectedLanguage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing audio for POI {Name}", poi.Name);
            await Shell.Current.DisplayAlert("Lỗi", $"Không thể phát audio: {ex.Message}", "OK");
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
