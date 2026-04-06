using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VK.Mobile.Models;
using VK.Mobile.Resources.Strings;

namespace VK.Mobile.Services;

public interface IOfflineContentService
{
    Task<OfflinePackageResult> DownloadOfflinePackageAsync(
        string languageCode,
        bool includeAudioFiles = false,
        CancellationToken ct = default);

    Task AutoSyncWhenOnlineAsync(string languageCode, CancellationToken ct = default);

    Task<string?> GetCachedNarrationTextAsync(int poiId, string languageCode);

    Task CacheNarrationScriptAsync(
        int poiId,
        string languageCode,
        string textContent,
        string? audioFileUrl = null,
        int? durationInSeconds = null,
        string? localAudioPath = null);

    Task<OfflinePackageStatus> GetStatusAsync();
}

public record OfflinePackageResult(
    bool Success,
    string Message,
    int PoiCount,
    int ScriptCount,
    int AudioFileCount);

public record OfflinePackageStatus(
    DateTime? LastSyncUtc,
    int PoiCount,
    int ScriptCount,
    int AudioFileCount,
    string LanguageCode);

public class OfflineContentService : IOfflineContentService
{
    private const string KeyLastSyncTicks = "Offline.LastSyncTicks";
    private const string KeyPoiCount = "Offline.PoiCount";
    private const string KeyScriptCount = "Offline.ScriptCount";
    private const string KeyAudioFileCount = "Offline.AudioFileCount";
    private const string KeyLanguage = "Offline.Language";

    private readonly IApiService _apiService;
    private readonly LocalPOIDatabase _localDb;
    private readonly ILogger<OfflineContentService> _logger;
    private readonly HttpClient _downloadClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string OfflineRouteFolderName = "offline_routes";
    private const int RouteGraphVersion = 2;
    private static readonly JsonSerializerOptions RoutePackageJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OfflineContentService(
        IApiService apiService,
        LocalPOIDatabase localDb,
        ILogger<OfflineContentService> logger)
    {
        _apiService = apiService;
        _localDb = localDb;
        _logger = logger;
    }

    public async Task<OfflinePackageResult> DownloadOfflinePackageAsync(
        string languageCode,
        bool includeAudioFiles = false,
        CancellationToken ct = default)
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return new OfflinePackageResult(
                false,
                GetLocalizedString("OfflineNoInternet", languageCode),
                0,
                0,
                0);
        }

        try
        {
            var lang = NormalizeLanguage(languageCode);
            var pois = await _apiService.GetAllPOIsAsync(languageCode: lang);

            if (pois.Count == 0)
            {
                return new OfflinePackageResult(
                    false,
                    GetLocalizedString("OfflinePoiFetchFailed", lang),
                    0,
                    0,
                    0);
            }

            await _localDb.SavePOIsAsync(pois, lang);

            var scriptCount = 0;
            var audioFileCount = 0;

            foreach (var poi in pois)
            {
                ct.ThrowIfCancellationRequested();

                AudioContentResult? audio = null;
                try
                {
                    audio = await _apiService.GetAudioForPOIAsync(poi.Id, lang);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skip audio fetch for POI {PoiId}", poi.Id);
                }

                var narrationText = !string.IsNullOrWhiteSpace(audio?.TextContent)
                    ? audio!.TextContent!
                    : (!string.IsNullOrWhiteSpace(poi.Description)
                        ? $"{poi.Name}. {poi.Description}"
                        : poi.Name);

                string? localAudioPath = null;
                if (includeAudioFiles && !string.IsNullOrWhiteSpace(audio?.AudioFileUrl))
                {
                    localAudioPath = await DownloadAudioFileAsync(
                        audio!.AudioFileUrl!,
                        poi.Id,
                        audio.LanguageCode ?? lang,
                        ct);

                    if (!string.IsNullOrWhiteSpace(localAudioPath))
                        audioFileCount++;
                }

                await _localDb.SaveAudioScriptAsync(
                    poi.Id,
                    audio?.LanguageCode ?? lang,
                    narrationText,
                    audio?.AudioFileUrl,
                    audio?.DurationInSeconds,
                    localAudioPath);

                scriptCount++;
            }

            var now = DateTime.UtcNow;
            Preferences.Set(KeyLastSyncTicks, now.Ticks);
            Preferences.Set(KeyPoiCount, pois.Count);
            Preferences.Set(KeyScriptCount, scriptCount);
            Preferences.Set(KeyAudioFileCount, audioFileCount);
            Preferences.Set(KeyLanguage, lang);

            // Pre-cache OSM tiles cho khu vực Vĩnh Khánh (zoom 14-17)
            _ = Task.Run(() => PreCacheMapTilesAsync(ct), ct);

            var routePackageDownloaded = await DownloadRoutePackageAsync(ct);
            if (routePackageDownloaded && !IsLocalRoutePackageCompatible())
            {
                routePackageDownloaded = false;
            }

            if (!routePackageDownloaded)
            {
                if (IsLocalRoutePackageCompatible())
                {
                    routePackageDownloaded = true;
                }
                else
                {
                    routePackageDownloaded = await BuildRoutePackageFromOsrmAsync(pois, ct)
                        || IsLocalRoutePackageCompatible();
                }
            }

            var culture = ResolveCulture(lang);
            var audioSuffix = audioFileCount > 0
                ? string.Format(
                    culture,
                    GetLocalizedString("OfflineDownloadAudioSuffixFormat", lang),
                    audioFileCount)
                : string.Empty;

            var routeSuffix = routePackageDownloaded
                ? GetLocalizedString("OfflineDownloadRouteSuffix", lang)
                : string.Empty;

            var message = string.Format(
                culture,
                GetLocalizedString("OfflineDownloadSummaryFormat", lang),
                pois.Count,
                scriptCount,
                audioSuffix,
                routeSuffix);

            return new OfflinePackageResult(true, message, pois.Count, scriptCount, audioFileCount);
        }
        catch (OperationCanceledException)
        {
            return new OfflinePackageResult(false, GetLocalizedString("OfflineDownloadCancelled", languageCode), 0, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadOfflinePackageAsync failed");
            return new OfflinePackageResult(false, GetLocalizedString("OfflineDownloadFailed", languageCode), 0, 0, 0);
        }
    }

    public async Task AutoSyncWhenOnlineAsync(string languageCode, CancellationToken ct = default)
    {
        try
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                return;

            var status = await GetStatusAsync();
            if (status.LastSyncUtc.HasValue &&
                DateTime.UtcNow - status.LastSyncUtc.Value < TimeSpan.FromMinutes(30))
            {
                return;
            }

            await DownloadOfflinePackageAsync(languageCode, includeAudioFiles: false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AutoSyncWhenOnlineAsync skipped due to error");
        }
    }

    public Task<string?> GetCachedNarrationTextAsync(int poiId, string languageCode)
        => _localDb.GetCachedNarrationTextAsync(poiId, languageCode);

    public Task CacheNarrationScriptAsync(
        int poiId,
        string languageCode,
        string textContent,
        string? audioFileUrl = null,
        int? durationInSeconds = null,
        string? localAudioPath = null)
        => _localDb.SaveAudioScriptAsync(
            poiId,
            languageCode,
            textContent,
            audioFileUrl,
            durationInSeconds,
            localAudioPath);

    public async Task<OfflinePackageStatus> GetStatusAsync()
    {
        var ticks = Preferences.Get(KeyLastSyncTicks, 0L);
        DateTime? lastSync = ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;

        var poiCount = Preferences.Get(KeyPoiCount, -1);
        if (poiCount < 0)
            poiCount = await _localDb.GetCachedPoiCountAsync();

        var scriptCount = Preferences.Get(KeyScriptCount, -1);
        if (scriptCount < 0)
            scriptCount = await _localDb.GetAudioScriptCountAsync();

        var audioFileCount = Preferences.Get(KeyAudioFileCount, 0);
        var languageCode = Preferences.Get(KeyLanguage, "vi");

        return new OfflinePackageStatus(
            lastSync,
            poiCount,
            scriptCount,
            audioFileCount,
            languageCode);
    }


    private async Task<string?> DownloadAudioFileAsync(
        string audioFileUrl,
        int poiId,
        string languageCode,
        CancellationToken ct)
    {
        try
        {
            var absoluteUrl = ToAbsoluteUrl(audioFileUrl);
            var uri = new Uri(absoluteUrl);

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".mp3";

            var folder = Path.Combine(FileSystem.AppDataDirectory, "offline_audio");
            Directory.CreateDirectory(folder);

            var fileName = $"poi_{poiId}_{NormalizeLanguage(languageCode)}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
                return fullPath;

            using var response = await _downloadClient.GetAsync(absoluteUrl, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(fullPath, bytes, ct);
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Download audio file failed for POI {PoiId}", poiId);
            return null;
        }
    }

    /// <summary>Pre-download OSM tiles cho khu vực Vĩnh Khánh (zoom 14-17) vào FileCache.</summary>
    private async Task PreCacheMapTilesAsync(CancellationToken ct)
    {
        // Bounding box khu vực Vĩnh Khánh, Quận 4, TP.HCM
        const double minLat = 10.758, maxLat = 10.765;
        const double minLon = 106.699, maxLon = 106.710;

        var tileCacheDir = Path.Combine(FileSystem.CacheDirectory, "osm_tiles");
        Directory.CreateDirectory(tileCacheDir);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VKStreetFood/1.0 (offline-cache)");

        int downloaded = 0, skipped = 0;
        for (int zoom = 14; zoom <= 17; zoom++)
        {
            var (x0, y0) = LatLonToTileXY(maxLat, minLon, zoom);
            var (x1, y1) = LatLonToTileXY(minLat, maxLon, zoom);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (ct.IsCancellationRequested) return;

                    // FileCache path: {dir}/{zoom}/{x}/{y}.png
                    var tilePath = Path.Combine(tileCacheDir, zoom.ToString(), x.ToString(), $"{y}.png");
                    if (File.Exists(tilePath) && new FileInfo(tilePath).Length > 0)
                    { skipped++; continue; }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
                        var url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
                        var bytes = await http.GetByteArrayAsync(url, ct);
                        await File.WriteAllBytesAsync(tilePath, bytes, ct);
                        downloaded++;
                        await Task.Delay(100, ct); // Respect OSM rate limit
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Skip tile {z}/{x}/{y}", zoom, x, y);
                    }
                }
            }
        }
        _logger.LogInformation("[TileCache] Pre-cached {d} tiles, skipped {s}", downloaded, skipped);
    }

    private static (int x, int y) LatLonToTileXY(double lat, double lon, int zoom)
    {
        int n = (int)Math.Pow(2, zoom);
        int x = (int)((lon + 180.0) / 360.0 * n);
        double latRad = lat * Math.PI / 180.0;
        int y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (x, y);
    }

    private static string NormalizeLanguage(string languageCode)
        => string.IsNullOrWhiteSpace(languageCode)
            ? "vi"
            : languageCode.Trim().ToLowerInvariant();

    private static CultureInfo ResolveCulture(string languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        return normalized switch
        {
            "en" => new CultureInfo("en-US"),
            "ko" => new CultureInfo("ko-KR"),
            _ => new CultureInfo("vi-VN")
        };
    }

    private static string GetLocalizedString(string key, string languageCode)
        => AppResources.ResourceManager.GetString(key, ResolveCulture(languageCode)) ?? key;

    private static string ToAbsoluteUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUrl = AppSettings.AudioBaseUrl.TrimEnd('/');
        if (url.StartsWith('/'))
            return baseUrl + url;

        return $"{baseUrl}/{url}";
    }

    private async Task<bool> DownloadRoutePackageAsync(CancellationToken ct)
    {
        try
        {
            var routePackageUrl =
                $"{AppSettings.ApiBaseUrl.TrimEnd('/')}/{AppSettings.OfflineRoutePackageRelativeUrl}";

            using var response = await _downloadClient.GetAsync(routePackageUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Route package download skipped: status {StatusCode}",
                    (int)response.StatusCode);
                return false;
            }

            var folder = Path.Combine(FileSystem.AppDataDirectory, OfflineRouteFolderName);
            Directory.CreateDirectory(folder);

            var filePath = Path.Combine(folder, AppSettings.OfflineRoutePackageFileName);
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(filePath);
            await source.CopyToAsync(target, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Route package download failed");
            return false;
        }
    }

    private async Task<bool> BuildRoutePackageFromOsrmAsync(IReadOnlyList<POIModel> pois, CancellationToken ct)
    {
        try
        {
            var validPois = pois
                .Where(poi => poi.Latitude != 0 || poi.Longitude != 0)
                .ToList();

            if (validPois.Count < 2)
                return false;

            var waypoints = BuildRoutingWaypoints(validPois);
            if (waypoints.Count < 2)
                return false;

            var pairKeys = new HashSet<string>(StringComparer.Ordinal);
            var selectedPairs = new List<(RoutingWaypoint FromWaypoint, RoutingWaypoint ToWaypoint)>();

            foreach (var fromWaypoint in waypoints)
            {
                var neighborsPerWaypoint = fromWaypoint.IsAnchor ? 4 : 7;
                var nearestNeighbors = waypoints
                    .Where(waypoint => waypoint.Id != fromWaypoint.Id)
                    .OrderBy(waypoint => CalculateHaversineDistanceMeters(
                        fromWaypoint.Latitude,
                        fromWaypoint.Longitude,
                        waypoint.Latitude,
                        waypoint.Longitude))
                    .Take(neighborsPerWaypoint)
                    .ToList();

                foreach (var toWaypoint in nearestNeighbors)
                {
                    var minId = Math.Min(fromWaypoint.Id, toWaypoint.Id);
                    var maxId = Math.Max(fromWaypoint.Id, toWaypoint.Id);
                    var pairKey = $"{minId}:{maxId}";

                    if (!pairKeys.Add(pairKey))
                        continue;

                    selectedPairs.Add((fromWaypoint, toWaypoint));
                }
            }

            if (selectedPairs.Count == 0)
                return false;

            const int maxRoutePairs = 45;
            if (selectedPairs.Count > maxRoutePairs)
            {
                selectedPairs = selectedPairs
                    .OrderBy(pair => CalculateHaversineDistanceMeters(
                        pair.FromWaypoint.Latitude,
                        pair.FromWaypoint.Longitude,
                        pair.ToWaypoint.Latitude,
                        pair.ToWaypoint.Longitude))
                    .Take(maxRoutePairs)
                    .ToList();
            }

            var routeBuilder = new RouteGraphBuilder();
            var successfulRouteCount = 0;

            foreach (var (fromWaypoint, toWaypoint) in selectedPairs)
            {
                ct.ThrowIfCancellationRequested();

                var routeCoordinates = await FetchOsrmRouteCoordinatesAsync(
                    fromWaypoint.Latitude,
                    fromWaypoint.Longitude,
                    toWaypoint.Latitude,
                    toWaypoint.Longitude,
                    ct);

                if (routeCoordinates == null || routeCoordinates.Count < 2)
                    continue;

                routeBuilder.AddRoute(routeCoordinates);
                successfulRouteCount++;
            }

            if (!routeBuilder.HasData)
                return false;

            var routePackage = routeBuilder.BuildPackage();
            var filePath = GetRoutePackagePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await using var output = File.Create(filePath);
            await JsonSerializer.SerializeAsync(output, routePackage, RoutePackageJsonOptions, ct);

            _logger.LogInformation(
                "Offline route graph generated from OSRM: {NodeCount} nodes, {EdgeCount} edges, {RouteCount} routes, {WaypointCount} waypoints",
                routePackage.Nodes.Count,
                routePackage.Edges.Count,
                successfulRouteCount,
                waypoints.Count);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BuildRoutePackageFromOsrmAsync failed");
            return false;
        }
    }

    private async Task<List<RoutePointModel>?> FetchOsrmRouteCoordinatesAsync(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        CancellationToken ct)
    {
        try
        {
            var fromLon = fromLongitude.ToString(CultureInfo.InvariantCulture);
            var fromLat = fromLatitude.ToString(CultureInfo.InvariantCulture);
            var toLon = toLongitude.ToString(CultureInfo.InvariantCulture);
            var toLat = toLatitude.ToString(CultureInfo.InvariantCulture);

            var url =
                $"{AppSettings.OsrmBaseUrl.TrimEnd('/')}/route/v1/driving/{fromLon},{fromLat};{toLon},{toLat}?overview=full&geometries=polyline&steps=false";

            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestCts.CancelAfter(TimeSpan.FromSeconds(8));

            using var response = await _downloadClient.GetAsync(url, requestCts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(requestCts.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: requestCts.Token);

            var root = document.RootElement;
            if (!root.TryGetProperty("code", out var codeElement) ||
                !string.Equals(codeElement.GetString(), "Ok", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("routes", out var routesElement) || routesElement.GetArrayLength() == 0)
                return null;

            var routeElement = routesElement[0];
            if (!routeElement.TryGetProperty("geometry", out var geometryElement))
                return null;

            var encodedPolyline = geometryElement.GetString();
            if (string.IsNullOrWhiteSpace(encodedPolyline))
                return null;

            return DecodePolyline(encodedPolyline);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "FetchOsrmRouteCoordinatesAsync failed for [{FromLat},{FromLon}] -> [{ToLat},{ToLon}]",
                fromLatitude,
                fromLongitude,
                toLatitude,
                toLongitude);
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

    private static bool IsLocalRoutePackageCompatible()
    {
        var path = GetRoutePackagePath();
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("graphVersion", out var versionElement))
                return false;

            return versionElement.ValueKind == JsonValueKind.Number
                && versionElement.TryGetInt32(out var graphVersion)
                && graphVersion >= RouteGraphVersion;
        }
        catch
        {
            return false;
        }
    }

    private static string GetRoutePackagePath()
    {
        return Path.Combine(
            FileSystem.AppDataDirectory,
            OfflineRouteFolderName,
            AppSettings.OfflineRoutePackageFileName);
    }

    private static List<RoutingWaypoint> BuildRoutingWaypoints(IReadOnlyList<POIModel> validPois)
    {
        var waypoints = new List<RoutingWaypoint>(validPois.Count + 20);

        foreach (var poi in validPois)
        {
            waypoints.Add(new RoutingWaypoint(poi.Id, poi.Latitude, poi.Longitude, false));
        }

        var minLatitude = validPois.Min(poi => poi.Latitude);
        var maxLatitude = validPois.Max(poi => poi.Latitude);
        var minLongitude = validPois.Min(poi => poi.Longitude);
        var maxLongitude = validPois.Max(poi => poi.Longitude);

        const double margin = 0.0015;
        minLatitude -= margin;
        maxLatitude += margin;
        minLongitude -= margin;
        maxLongitude += margin;

        const int latStepCount = 4;
        const int lonStepCount = 4;

        var latitudeStep = latStepCount > 1
            ? (maxLatitude - minLatitude) / (latStepCount - 1)
            : 0;

        var longitudeStep = lonStepCount > 1
            ? (maxLongitude - minLongitude) / (lonStepCount - 1)
            : 0;

        var anchorId = -1;
        for (var latIndex = 0; latIndex < latStepCount; latIndex++)
        {
            for (var lonIndex = 0; lonIndex < lonStepCount; lonIndex++)
            {
                var latitude = minLatitude + latIndex * latitudeStep;
                var longitude = minLongitude + lonIndex * longitudeStep;

                waypoints.Add(new RoutingWaypoint(anchorId--, latitude, longitude, true));
            }
        }

        return waypoints;
    }

    private sealed record RoutingWaypoint(int Id, double Latitude, double Longitude, bool IsAnchor);

    private sealed class RouteGraphBuilder
    {
        private readonly Dictionary<string, int> _nodeIdByKey = new(StringComparer.Ordinal);
        private readonly List<OfflineRouteNodeDto> _nodes = new();
        private readonly Dictionary<string, OfflineRouteEdgeDto> _edgesByKey = new(StringComparer.Ordinal);
        private int _nextNodeId = 1;

        public bool HasData => _nodes.Count > 1 && _edgesByKey.Count > 0;

        public void AddRoute(IReadOnlyList<RoutePointModel> routeCoordinates)
        {
            if (routeCoordinates.Count < 2)
                return;

            for (var index = 1; index < routeCoordinates.Count; index++)
            {
                var fromPoint = NormalizePoint(routeCoordinates[index - 1]);
                var toPoint = NormalizePoint(routeCoordinates[index]);

                var fromNodeId = GetOrAddNode(fromPoint);
                var toNodeId = GetOrAddNode(toPoint);
                if (fromNodeId == toNodeId)
                    continue;

                var edgeKey = BuildEdgeKey(fromNodeId, toNodeId);
                if (_edgesByKey.ContainsKey(edgeKey))
                    continue;

                _edgesByKey[edgeKey] = new OfflineRouteEdgeDto
                {
                    FromNodeId = fromNodeId,
                    ToNodeId = toNodeId,
                    Bidirectional = true,
                    DistanceMeters = CalculateHaversineDistanceMeters(
                        fromPoint.Latitude,
                        fromPoint.Longitude,
                        toPoint.Latitude,
                        toPoint.Longitude),
                    Shape = new List<RoutePointModel>
                    {
                        new() { Latitude = fromPoint.Latitude, Longitude = fromPoint.Longitude },
                        new() { Latitude = toPoint.Latitude, Longitude = toPoint.Longitude }
                    }
                };
            }
        }

        public OfflineRoutePackageDto BuildPackage()
        {
            return new OfflineRoutePackageDto
            {
                GraphVersion = RouteGraphVersion,
                Nodes = _nodes,
                Edges = _edgesByKey.Values.ToList()
            };
        }

        private int GetOrAddNode(RoutePointModel point)
        {
            var key = BuildNodeKey(point);
            if (_nodeIdByKey.TryGetValue(key, out var nodeId))
                return nodeId;

            nodeId = _nextNodeId++;
            _nodeIdByKey[key] = nodeId;
            _nodes.Add(new OfflineRouteNodeDto
            {
                Id = nodeId,
                Latitude = point.Latitude,
                Longitude = point.Longitude
            });

            return nodeId;
        }

        private static RoutePointModel NormalizePoint(RoutePointModel point)
        {
            return new RoutePointModel
            {
                Latitude = Math.Round(point.Latitude, 5),
                Longitude = Math.Round(point.Longitude, 5)
            };
        }

        private static string BuildNodeKey(RoutePointModel point)
        {
            return $"{point.Latitude:F5},{point.Longitude:F5}";
        }

        private static string BuildEdgeKey(int fromNodeId, int toNodeId)
        {
            return fromNodeId < toNodeId
                ? $"{fromNodeId}-{toNodeId}"
                : $"{toNodeId}-{fromNodeId}";
        }
    }

    private sealed class OfflineRoutePackageDto
    {
        public int GraphVersion { get; set; } = RouteGraphVersion;
        public List<OfflineRouteNodeDto> Nodes { get; set; } = new();
        public List<OfflineRouteEdgeDto> Edges { get; set; } = new();
    }

    private sealed class OfflineRouteNodeDto
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class OfflineRouteEdgeDto
    {
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }
        public double? DistanceMeters { get; set; }
        public bool Bidirectional { get; set; } = true;
        public List<RoutePointModel> Shape { get; set; } = new();
    }
}
