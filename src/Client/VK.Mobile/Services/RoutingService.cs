using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IRoutingService
{
    Task<RouteResultModel?> GetDrivingRouteAsync(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        CancellationToken cancellationToken = default);
}

public class OsrmRoutingService : IRoutingService
{
    private readonly ILogger<OsrmRoutingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, RouteResultModel> _routeCache = new();

    private const double DefaultWalkSpeedMetersPerSecond = 1.2;

    public OsrmRoutingService(ILogger<OsrmRoutingService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    public async Task<RouteResultModel?> GetDrivingRouteAsync(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey(fromLatitude, fromLongitude, toLatitude, toLongitude);

            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                if (_routeCache.TryGetValue(cacheKey, out var cachedRoute))
                {
                    _logger.LogInformation("Offline mode: use cached route");
                    return CloneRoute(cachedRoute, "cache");
                }

                _logger.LogInformation("Offline mode: no cached route, use straight-line fallback");
                return BuildOfflineFallbackRoute(fromLatitude, fromLongitude, toLatitude, toLongitude);
            }

            var fromLon = fromLongitude.ToString(CultureInfo.InvariantCulture);
            var fromLat = fromLatitude.ToString(CultureInfo.InvariantCulture);
            var toLon = toLongitude.ToString(CultureInfo.InvariantCulture);
            var toLat = toLatitude.ToString(CultureInfo.InvariantCulture);

            var url =
                $"{AppSettings.OsrmBaseUrl.TrimEnd('/')}/route/v1/driving/{fromLon},{fromLat};{toLon},{toLat}?overview=full&geometries=polyline&steps=false";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM route request failed with status {StatusCode}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = document.RootElement;
            if (!root.TryGetProperty("code", out var codeElement) ||
                !string.Equals(codeElement.GetString(), "Ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("OSRM returned non-Ok code");
                return null;
            }

            if (!root.TryGetProperty("routes", out var routesElement) || routesElement.GetArrayLength() == 0)
            {
                _logger.LogWarning("OSRM returned empty routes");
                return null;
            }

            var routeElement = routesElement[0];

            if (!routeElement.TryGetProperty("geometry", out var geometryElement))
            {
                _logger.LogWarning("OSRM route missing geometry");
                return null;
            }

            var encodedPolyline = geometryElement.GetString();
            if (string.IsNullOrWhiteSpace(encodedPolyline))
            {
                _logger.LogWarning("OSRM geometry is empty");
                return null;
            }

            var decodedCoordinates = DecodePolyline(encodedPolyline);
            if (decodedCoordinates.Count < 2)
            {
                _logger.LogWarning("OSRM decoded route has fewer than 2 points");
                return null;
            }

            var distanceMeters = routeElement.TryGetProperty("distance", out var distanceElement)
                ? distanceElement.GetDouble()
                : 0;

            var durationSeconds = routeElement.TryGetProperty("duration", out var durationElement)
                ? durationElement.GetDouble()
                : 0;

            var result = new RouteResultModel
            {
                Coordinates = decodedCoordinates,
                DistanceMeters = distanceMeters,
                DurationSeconds = durationSeconds,
                Provider = "osrm"
            };

            _routeCache[cacheKey] = CloneRoute(result, "osrm");
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OSRM routing request canceled");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OSRM route API");
            return null;
        }
    }

    private static List<RoutePointModel> DecodePolyline(string encodedPolyline, int precision = 5)
    {
        var decodedPoints = new List<RoutePointModel>();
        var index = 0;
        var latitude = 0;
        var longitude = 0;
        var factor = Math.Pow(10, precision);

        while (index < encodedPolyline.Length)
        {
            var latitudeDelta = DecodeComponent(encodedPolyline, ref index);
            var longitudeDelta = DecodeComponent(encodedPolyline, ref index);

            latitude += latitudeDelta;
            longitude += longitudeDelta;

            decodedPoints.Add(new RoutePointModel
            {
                Latitude = latitude / factor,
                Longitude = longitude / factor
            });
        }

        return decodedPoints;
    }

    private static int DecodeComponent(string encodedPolyline, ref int index)
    {
        var result = 0;
        var shift = 0;

        while (index < encodedPolyline.Length)
        {
            var value = encodedPolyline[index++] - 63;
            result |= (value & 0x1f) << shift;
            shift += 5;

            if (value < 0x20)
                break;
        }

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }

    private static string BuildCacheKey(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        return $"{Math.Round(fromLatitude, 5)},{Math.Round(fromLongitude, 5)}->{Math.Round(toLatitude, 5)},{Math.Round(toLongitude, 5)}";
    }

    private static RouteResultModel CloneRoute(RouteResultModel source, string provider)
    {
        return new RouteResultModel
        {
            Coordinates = source.Coordinates
                .Select(point => new RoutePointModel
                {
                    Latitude = point.Latitude,
                    Longitude = point.Longitude
                })
                .ToList(),
            DistanceMeters = source.DistanceMeters,
            DurationSeconds = source.DurationSeconds,
            Provider = provider
        };
    }

    private static RouteResultModel BuildOfflineFallbackRoute(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude)
    {
        var distanceMeters = CalculateHaversineDistanceMeters(fromLatitude, fromLongitude, toLatitude, toLongitude);
        var durationSeconds = distanceMeters / DefaultWalkSpeedMetersPerSecond;

        return new RouteResultModel
        {
            Coordinates = new List<RoutePointModel>
            {
                new() { Latitude = fromLatitude, Longitude = fromLongitude },
                new() { Latitude = toLatitude, Longitude = toLongitude }
            },
            DistanceMeters = distanceMeters,
            DurationSeconds = durationSeconds,
            Provider = "offline-fallback"
        };
    }

    private static double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;

        var deltaLat = ToRadians(lat2 - lat1);
        var deltaLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distanceKm = earthRadiusKm * c;

        return distanceKm * 1000;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}