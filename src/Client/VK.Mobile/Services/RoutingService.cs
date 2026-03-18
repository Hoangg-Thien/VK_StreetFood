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
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                _logger.LogInformation("Skip OSRM routing in offline mode");
                return null;
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

            return new RouteResultModel
            {
                Coordinates = decodedCoordinates,
                DistanceMeters = distanceMeters,
                DurationSeconds = durationSeconds,
                Provider = "osrm"
            };
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
}