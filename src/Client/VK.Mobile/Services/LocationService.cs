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

    public event EventHandler<LocationChangedEventArgs>? LocationChanged;
    public bool IsTracking => _isTracking;

    public LocationService(ILogger<LocationService> logger, IApiService apiService)
    {
        _logger = logger;
        _apiService = apiService;
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

                    await Task.Delay(AppSettings.LocationUpdateIntervalSeconds * 1000, _cts.Token);
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
        return Task.CompletedTask;
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
