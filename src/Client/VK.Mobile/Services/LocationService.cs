using VK.Mobile.Models;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.Services;

public interface ILocationService
{
    event EventHandler<LocationChangedEventArgs>? LocationChanged;
    Task<Location?> GetCurrentLocationAsync();
    Task StartTrackingAsync();
    Task StopTrackingAsync();
    bool IsTracking { get; }
    double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    /// <summary>Cập nhật interval GPS (ms) động theo tốc độ di chuyển.</summary>
    void SetUpdateInterval(int intervalMs);
}

public class LocationChangedEventArgs : EventArgs
{
    public Location Location { get; set; } = null!;
    public List<POIModel> NearbyPOIs { get; set; } = new();
}

public class LocationService : ILocationService
{
    private readonly ILogger<LocationService> _logger;
    private readonly IApiService _apiService;
    private CancellationTokenSource? _cts;
    private bool _isTracking;
    private int _updateIntervalMs = AppSettings.LocationUpdateIntervalSeconds * 1000;

    // Battery optimization: theo dõi vị trí cuối để tính speed
    private Location? _lastLocation;
    private DateTime _lastLocationTime;

    public event EventHandler<LocationChangedEventArgs>? LocationChanged;
    public bool IsTracking => _isTracking;

    public LocationService(ILogger<LocationService> logger, IApiService apiService)
    {
        _logger = logger;
        _apiService = apiService;
    }

    public void SetUpdateInterval(int intervalMs)
    {
        _updateIntervalMs = Math.Max(3000, intervalMs); // tối thiểu 3 giây
        _logger.LogInformation("GPS update interval set to {Ms}ms", _updateIntervalMs);
    }

    public async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            // Check and request location permissions
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    _logger.LogWarning("Location permission not granted");
                    return null;
                }
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.GetLocationAsync(request);

            if (location != null)
            {
                _logger.LogInformation("Location: {Lat}, {Lon}", location.Latitude, location.Longitude);
            }
            else
            {
                _logger.LogWarning("Location is null, trying last known location");
                location = await Geolocation.GetLastKnownLocationAsync();
            }

            return location;
        }
        catch (FeatureNotSupportedException ex)
        {
            _logger.LogError(ex, "Location feature not supported on this device");
            return null;
        }
        catch (PermissionException ex)
        {
            _logger.LogError(ex, "Location permission denied");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location");
            return null;
        }
    }

    public async Task StartTrackingAsync()
    {
        if (_isTracking)
            return;

        // Yêu cầu quyền background location trước khi bắt đầu
        await RequestBackgroundLocationPermissionAsync();

        // Khởi động Android Foreground Service (persistent notification, tránh bị OS kill)
#if ANDROID
        VK.Mobile.Platforms.Android.LocationForegroundService.Start(
            global::Android.App.Application.Context);
        _logger.LogInformation("Android LocationForegroundService started");
#endif

        _isTracking = true;
        _cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var location = await GetCurrentLocationAsync();

                    if (location != null)
                    {
                        // Get nearby POIs (with error handling)
                        try
                        {
                            var nearbyPOIs = await _apiService.GetNearbyPOIsAsync(
                                location.Latitude,
                                location.Longitude,
                                AppSettings.GeofenceRadiusMeters / 1000.0);

                            LocationChanged?.Invoke(this, new LocationChangedEventArgs
                            {
                                Location = location,
                                NearbyPOIs = nearbyPOIs
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not get nearby POIs, continuing tracking");

                            // Still fire event with location but empty POIs
                            LocationChanged?.Invoke(this, new LocationChangedEventArgs
                            {
                                Location = location,
                                NearbyPOIs = new List<POIModel>()
                            });
                        }
                    }

                    // Battery optimization: tự điều chỉnh interval theo tốc độ
                    AdaptUpdateInterval(location);
                    await Task.Delay(_updateIntervalMs, _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in location tracking");
                    await Task.Delay(5000, _cts.Token); // Wait before retry
                }
            }
        }, _cts.Token);

        await Task.CompletedTask;
    }

    public Task StopTrackingAsync()
    {
        _cts?.Cancel();
        _isTracking = false;

        // Dừng Android Foreground Service
#if ANDROID
        VK.Mobile.Platforms.Android.LocationForegroundService.Stop(
            global::Android.App.Application.Context);
        _logger.LogInformation("Android LocationForegroundService stopped");
#endif

        return Task.CompletedTask;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static async Task RequestBackgroundLocationPermissionAsync()
    {
        // Foreground first
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

#if ANDROID
        // Android 10+ yêu cầu quyền background riêng (phải xin sau foreground)
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Q)
        {
            var bgStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (bgStatus != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.LocationAlways>();
        }
#endif

#if IOS
        // iOS: request Always authorization để background tracking
        var bgStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (bgStatus != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.LocationAlways>();
#endif
    }

    /// <summary>
    /// Điều chỉnh GPS polling interval theo tốc độ di chuyển:
    /// - Đứng yên / chậm (&lt;1 km/h): 30s → tiết kiệm pin
    /// - Đi bộ (1-5 km/h): 10s
    /// - Di chuyển nhanh (>5 km/h): 5s
    /// </summary>
    private void AdaptUpdateInterval(Location? newLocation)
    {
        if (newLocation == null || _lastLocation == null)
        {
            _lastLocation = newLocation;
            _lastLocationTime = DateTime.UtcNow;
            return;
        }

        var elapsed = (DateTime.UtcNow - _lastLocationTime).TotalHours;
        if (elapsed <= 0) return;

        var distanceKm = CalculateDistance(
            _lastLocation.Latitude, _lastLocation.Longitude,
            newLocation.Latitude, newLocation.Longitude);

        var speedKmh = distanceKm / elapsed;

        _updateIntervalMs = speedKmh switch
        {
            < 1.0 => 30_000,  // đứng yên
            < 5.0 => 10_000,  // đi bộ
            _ => 5_000   // di chuyển
        };

        _lastLocation = newLocation;
        _lastLocationTime = DateTime.UtcNow;

        _logger.LogDebug("Speed {Speed:F1} km/h → GPS interval {Ms}ms", speedKmh, _updateIntervalMs);
    }

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
}
