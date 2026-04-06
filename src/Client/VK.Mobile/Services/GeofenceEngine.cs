using Microsoft.Extensions.Logging;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IGeofenceEngine
{
    void MarkTrackingStarted(DateTime? startedAtUtc = null);

    GeofenceSelectionResult SelectCandidates(
        Location currentLocation,
        IEnumerable<POIModel>? nearbyApiPois,
        IEnumerable<POIModel>? fallbackPois,
        double defaultRadiusMeters,
        Func<double, double, double, double, double> calculateDistanceKm);

    bool ShouldTrigger(POIModel poi, DateTime? nowUtc = null);

    void Reset();
}

public sealed record GeofenceSelectionResult(IReadOnlyList<POIModel> NearbyPois, POIModel? BestCandidate);

public class GeofenceEngine : IGeofenceEngine
{
    private readonly ILogger<GeofenceEngine> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<int, DateTime> _lastTriggeredUtcByPoi = new();
    private DateTime _trackingStartedUtc = DateTime.MaxValue;

    public GeofenceEngine(ILogger<GeofenceEngine> logger)
    {
        _logger = logger;
    }

    public void MarkTrackingStarted(DateTime? startedAtUtc = null)
    {
        lock (_sync)
        {
            _trackingStartedUtc = startedAtUtc ?? DateTime.UtcNow;
        }
    }

    public GeofenceSelectionResult SelectCandidates(
        Location currentLocation,
        IEnumerable<POIModel>? nearbyApiPois,
        IEnumerable<POIModel>? fallbackPois,
        double defaultRadiusMeters,
        Func<double, double, double, double, double> calculateDistanceKm)
    {
        var nearbyFromApi = (nearbyApiPois ?? Enumerable.Empty<POIModel>())
            .Where(HasValidCoordinates)
            .ToList();

        List<POIModel> workingNearby;

        if (nearbyFromApi.Count > 0)
        {
            workingNearby = nearbyFromApi;
        }
        else
        {
            var fallbackSearchRadiusMeters = Math.Max(defaultRadiusMeters * 6.0, 1_000.0);

            workingNearby = (fallbackPois ?? Enumerable.Empty<POIModel>())
                .Where(HasValidCoordinates)
                .Select(poi => new
                {
                    Poi = poi,
                    DistanceMeters = calculateDistanceKm(
                        currentLocation.Latitude,
                        currentLocation.Longitude,
                        poi.Latitude,
                        poi.Longitude) * 1000.0
                })
                .Where(x => x.DistanceMeters <= fallbackSearchRadiusMeters)
                .OrderBy(x => x.DistanceMeters)
                .Take(40)
                .Select(x =>
                {
                    x.Poi.DistanceKm = x.DistanceMeters / 1000.0;
                    return x.Poi;
                })
                .ToList();
        }

        var deduped = new Dictionary<int, POIModel>();
        foreach (var poi in workingNearby)
        {
            if (poi.Id <= 0)
                continue;

            var distanceMeters = calculateDistanceKm(
                currentLocation.Latitude,
                currentLocation.Longitude,
                poi.Latitude,
                poi.Longitude) * 1000.0;

            poi.DistanceKm = distanceMeters / 1000.0;

            if (!deduped.ContainsKey(poi.Id))
                deduped.Add(poi.Id, poi);
        }

        var nearbyOrdered = deduped.Values
            .OrderBy(poi => poi.DistanceKm ?? double.MaxValue)
            .ToList();

        var bestCandidate = nearbyOrdered
            .Select(poi => new
            {
                Poi = poi,
                DistanceMeters = (poi.DistanceKm ?? double.MaxValue) * 1000.0,
                EffectiveRadiusMeters = ResolvePoiRadiusMeters(poi, defaultRadiusMeters)
            })
            .Where(x => x.DistanceMeters <= x.EffectiveRadiusMeters)
            .OrderByDescending(x => x.Poi.Priority)
            .ThenBy(x => x.DistanceMeters)
            .Select(x => x.Poi)
            .FirstOrDefault();

        return new GeofenceSelectionResult(nearbyOrdered, bestCandidate);
    }

    public bool ShouldTrigger(POIModel poi, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        lock (_sync)
        {
            if ((now - _trackingStartedUtc).TotalMilliseconds < AppSettings.GeofenceDebounceMs)
            {
                _logger.LogDebug("Geofence debounced for POI {PoiId}", poi.Id);
                return false;
            }

            if (_lastTriggeredUtcByPoi.TryGetValue(poi.Id, out var lastTriggeredUtc))
            {
                var nextAllowedTrigger = lastTriggeredUtc.AddMinutes(AppSettings.GeofenceCooldownMinutes);
                if (now < nextAllowedTrigger)
                {
                    _logger.LogDebug(
                        "Geofence cooldown active for POI {PoiId}, remaining {RemainingSeconds:F0}s",
                        poi.Id,
                        (nextAllowedTrigger - now).TotalSeconds);
                    return false;
                }
            }

            _lastTriggeredUtcByPoi[poi.Id] = now;
            return true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _lastTriggeredUtcByPoi.Clear();
            _trackingStartedUtc = DateTime.MaxValue;
        }
    }

    public static double ResolvePoiRadiusMeters(POIModel poi, double defaultRadiusMeters)
    {
        if (poi.TriggerRadiusMeters.HasValue && poi.TriggerRadiusMeters.Value > 0)
            return poi.TriggerRadiusMeters.Value;

        return defaultRadiusMeters;
    }

    private static bool HasValidCoordinates(POIModel poi)
        => poi.Latitude != 0 || poi.Longitude != 0;
}
