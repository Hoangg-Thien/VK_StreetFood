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
    private readonly SemaphoreSlim _offlineGraphLock = new(1, 1);
    private OfflineRouteGraph? _offlineGraph;
    private DateTime _offlineGraphLastWriteUtc = DateTime.MinValue;
    private long _offlineGraphFileLength = -1;

    private const string OfflineRouteFolderName = "offline_routes";
    private const int RequiredRouteGraphVersion = 2;
    private const double DefaultWalkSpeedMetersPerSecond = 1.2;

    private static readonly JsonSerializerOptions OfflineGraphJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
                var offlineRoute = await TryBuildOfflineGraphRouteAsync(
                    fromLatitude,
                    fromLongitude,
                    toLatitude,
                    toLongitude,
                    cancellationToken);

                if (offlineRoute != null)
                    return offlineRoute;

                _logger.LogInformation("Offline mode: no route graph available, use straight-line fallback");
                return BuildOfflineFallbackRoute(fromLatitude, fromLongitude, toLatitude, toLongitude);
            }

            return await RequestOsrmRouteAsync(
                fromLatitude,
                fromLongitude,
                toLatitude,
                toLongitude,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Routing request canceled");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while resolving route");
            return null;
        }
    }

    private async Task<RouteResultModel?> RequestOsrmRouteAsync(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        CancellationToken cancellationToken)
    {
        try
        {
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

    private async Task<RouteResultModel?> TryBuildOfflineGraphRouteAsync(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        CancellationToken cancellationToken)
    {
        var offlineGraph = await GetOfflineGraphAsync(cancellationToken);
        if (offlineGraph == null)
            return null;

        var startCandidates = FindNearestNodeCandidates(offlineGraph.Nodes.Values, fromLatitude, fromLongitude);
        var endCandidates = FindNearestNodeCandidates(offlineGraph.Nodes.Values, toLatitude, toLongitude);

        if (startCandidates.Count == 0 || endCandidates.Count == 0)
            return null;

        var bestTotalDistance = double.PositiveInfinity;
        List<IReadOnlyList<RoutePointModel>>? bestRouteSegments = null;

        foreach (var startCandidate in startCandidates)
        {
            var searchResult = FindShortestPaths(offlineGraph, startCandidate.NodeId);

            foreach (var endCandidate in endCandidates)
            {
                if (!searchResult.Distances.TryGetValue(endCandidate.NodeId, out var graphDistance) ||
                    double.IsInfinity(graphDistance))
                {
                    continue;
                }

                var candidateTotalDistance =
                    startCandidate.ConnectorDistanceMeters + graphDistance + endCandidate.ConnectorDistanceMeters;

                if (candidateTotalDistance >= bestTotalDistance)
                    continue;

                var candidateSegments = ReconstructPathSegments(
                    searchResult.PreviousEdges,
                    startCandidate.NodeId,
                    endCandidate.NodeId);

                if (candidateSegments == null)
                    continue;

                bestTotalDistance = candidateTotalDistance;
                bestRouteSegments = candidateSegments;
            }
        }

        if (bestRouteSegments == null)
            return null;

        var coordinates = StitchRouteCoordinates(
            fromLatitude,
            fromLongitude,
            toLatitude,
            toLongitude,
            bestRouteSegments);

        if (coordinates.Count < 2)
            return null;

        var distanceMeters = CalculatePolylineDistanceMeters(coordinates);
        var durationSeconds = distanceMeters / DefaultWalkSpeedMetersPerSecond;

        return new RouteResultModel
        {
            Coordinates = coordinates,
            DistanceMeters = distanceMeters,
            DurationSeconds = durationSeconds,
            Provider = "offline-graph"
        };
    }

    private async Task<OfflineRouteGraph?> GetOfflineGraphAsync(CancellationToken cancellationToken)
    {
        var packagePath = GetOfflineRoutePackagePath();
        var packageInfo = new FileInfo(packagePath);

        if (_offlineGraph != null &&
            packageInfo.Exists &&
            packageInfo.LastWriteTimeUtc == _offlineGraphLastWriteUtc &&
            packageInfo.Length == _offlineGraphFileLength)
        {
            return _offlineGraph;
        }

        await _offlineGraphLock.WaitAsync(cancellationToken);
        try
        {
            packageInfo.Refresh();
            if (_offlineGraph != null &&
                packageInfo.Exists &&
                packageInfo.LastWriteTimeUtc == _offlineGraphLastWriteUtc &&
                packageInfo.Length == _offlineGraphFileLength)
            {
                return _offlineGraph;
            }

            if (!packageInfo.Exists)
            {
                _offlineGraph = null;
                _offlineGraphLastWriteUtc = DateTime.MinValue;
                _offlineGraphFileLength = -1;
                _logger.LogDebug("Offline route package not found at {Path}", packagePath);
                return null;
            }

            await using var stream = packageInfo.OpenRead();
            var package = await JsonSerializer.DeserializeAsync<OfflineRoutePackage>(
                stream,
                OfflineGraphJsonOptions,
                cancellationToken);

            if (package == null ||
                package.GraphVersion < RequiredRouteGraphVersion ||
                package.Nodes.Count == 0 ||
                package.Edges.Count == 0)
            {
                _logger.LogWarning(
                    "Offline route package is empty/invalid or too old. Version={Version}, required={RequiredVersion}",
                    package?.GraphVersion ?? 0,
                    RequiredRouteGraphVersion);
                _offlineGraph = null;
                return null;
            }

            _offlineGraph = BuildOfflineGraph(package);
            _offlineGraphLastWriteUtc = packageInfo.LastWriteTimeUtc;
            _offlineGraphFileLength = packageInfo.Length;
            return _offlineGraph;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load offline route package");
            return null;
        }
        finally
        {
            _offlineGraphLock.Release();
        }
    }

    private static OfflineRouteGraph BuildOfflineGraph(OfflineRoutePackage package)
    {
        var nodes = package.Nodes
            .Where(node => node.Id != 0)
            .GroupBy(node => node.Id)
            .Select(group => group.First())
            .ToDictionary(node => node.Id, node => node);

        var adjacency = nodes.Keys.ToDictionary(nodeId => nodeId, _ => new List<OfflineAdjacencyEdge>());

        foreach (var edge in package.Edges)
        {
            if (!nodes.TryGetValue(edge.FromNodeId, out var fromNode) ||
                !nodes.TryGetValue(edge.ToNodeId, out var toNode))
            {
                continue;
            }

            var forwardShape = BuildEdgeShape(edge.Shape, fromNode, toNode);
            var forwardDistance = ResolveEdgeDistanceMeters(edge.DistanceMeters, forwardShape);

            adjacency[edge.FromNodeId].Add(
                new OfflineAdjacencyEdge(edge.ToNodeId, forwardDistance, forwardShape));

            if (edge.Bidirectional)
            {
                var reverseShape = forwardShape
                    .AsEnumerable()
                    .Reverse()
                    .Select(ClonePoint)
                    .ToList();

                adjacency[edge.ToNodeId].Add(
                    new OfflineAdjacencyEdge(edge.FromNodeId, forwardDistance, reverseShape));
            }
        }

        return new OfflineRouteGraph(nodes, adjacency);
    }

    private static List<RoutePointModel> BuildEdgeShape(
        List<RoutePointModel>? rawShape,
        OfflineRouteNode fromNode,
        OfflineRouteNode toNode)
    {
        var shape = (rawShape ?? new List<RoutePointModel>())
            .Select(ClonePoint)
            .ToList();

        var fromPoint = new RoutePointModel
        {
            Latitude = fromNode.Latitude,
            Longitude = fromNode.Longitude
        };

        var toPoint = new RoutePointModel
        {
            Latitude = toNode.Latitude,
            Longitude = toNode.Longitude
        };

        if (shape.Count == 0)
        {
            shape.Add(fromPoint);
            shape.Add(toPoint);
            return shape;
        }

        if (!AreCoordinatesClose(shape[0], fromPoint))
            shape.Insert(0, fromPoint);

        if (!AreCoordinatesClose(shape[^1], toPoint))
            shape.Add(toPoint);

        return shape;
    }

    private static double ResolveEdgeDistanceMeters(double? providedDistance, IReadOnlyList<RoutePointModel> shape)
    {
        if (providedDistance.HasValue && providedDistance.Value > 0)
            return providedDistance.Value;

        var distanceFromShape = CalculatePolylineDistanceMeters(shape);
        return Math.Max(distanceFromShape, 1);
    }

    private static List<NodeCandidate> FindNearestNodeCandidates(
        IEnumerable<OfflineRouteNode> nodes,
        double latitude,
        double longitude,
        int maxCandidates = 5)
    {
        return nodes
            .Select(node => new NodeCandidate(
                node.Id,
                CalculateHaversineDistanceMeters(latitude, longitude, node.Latitude, node.Longitude)))
            .OrderBy(candidate => candidate.ConnectorDistanceMeters)
            .Take(maxCandidates)
            .ToList();
    }

    private static PathSearchResult FindShortestPaths(OfflineRouteGraph graph, int startNodeId)
    {
        var distances = graph.Nodes.Keys.ToDictionary(nodeId => nodeId, _ => double.PositiveInfinity);
        var previousEdges = new Dictionary<int, (int ParentNodeId, OfflineAdjacencyEdge Edge)>();
        var queue = new PriorityQueue<int, double>();

        if (!distances.ContainsKey(startNodeId))
        {
            return new PathSearchResult(distances, previousEdges);
        }

        distances[startNodeId] = 0;
        queue.Enqueue(startNodeId, 0);

        while (queue.Count > 0)
        {
            queue.TryDequeue(out var currentNodeId, out var currentDistance);
            if (currentDistance > distances[currentNodeId])
                continue;

            if (!graph.Adjacency.TryGetValue(currentNodeId, out var edges) || edges.Count == 0)
                continue;

            foreach (var edge in edges)
            {
                var tentativeDistance = currentDistance + edge.DistanceMeters;
                if (tentativeDistance >= distances[edge.ToNodeId])
                    continue;

                distances[edge.ToNodeId] = tentativeDistance;
                previousEdges[edge.ToNodeId] = (currentNodeId, edge);
                queue.Enqueue(edge.ToNodeId, tentativeDistance);
            }
        }

        return new PathSearchResult(distances, previousEdges);
    }

    private static List<IReadOnlyList<RoutePointModel>>? ReconstructPathSegments(
        IReadOnlyDictionary<int, (int ParentNodeId, OfflineAdjacencyEdge Edge)> previousEdges,
        int startNodeId,
        int endNodeId)
    {
        if (startNodeId == endNodeId)
            return new List<IReadOnlyList<RoutePointModel>>();

        if (!previousEdges.ContainsKey(endNodeId))
            return null;

        var segmentStack = new Stack<IReadOnlyList<RoutePointModel>>();
        var walker = endNodeId;

        while (walker != startNodeId)
        {
            if (!previousEdges.TryGetValue(walker, out var step))
                return null;

            segmentStack.Push(step.Edge.Shape);
            walker = step.ParentNodeId;
        }

        return segmentStack.ToList();
    }

    private static List<RoutePointModel> StitchRouteCoordinates(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        IReadOnlyList<IReadOnlyList<RoutePointModel>> routeSegments)
    {
        if (routeSegments.Count == 0)
        {
            return new List<RoutePointModel>
            {
                new() { Latitude = fromLatitude, Longitude = fromLongitude },
                new() { Latitude = toLatitude, Longitude = toLongitude }
            };
        }

        var stitched = new List<RoutePointModel>
        {
            new() { Latitude = fromLatitude, Longitude = fromLongitude }
        };

        foreach (var segment in routeSegments)
        {
            AppendShape(stitched, segment);
        }

        AppendPointIfNeeded(stitched, new RoutePointModel
        {
            Latitude = toLatitude,
            Longitude = toLongitude
        });

        return stitched;
    }

    private static void AppendShape(List<RoutePointModel> target, IEnumerable<RoutePointModel> shape)
    {
        foreach (var point in shape)
        {
            AppendPointIfNeeded(target, point);
        }
    }

    private static void AppendPointIfNeeded(List<RoutePointModel> target, RoutePointModel point)
    {
        if (target.Count == 0)
        {
            target.Add(ClonePoint(point));
            return;
        }

        var lastPoint = target[^1];
        if (AreCoordinatesClose(lastPoint, point))
            return;

        target.Add(ClonePoint(point));
    }

    private static RoutePointModel ClonePoint(RoutePointModel point)
    {
        return new RoutePointModel
        {
            Latitude = point.Latitude,
            Longitude = point.Longitude
        };
    }

    private static bool AreCoordinatesClose(RoutePointModel firstPoint, RoutePointModel secondPoint)
    {
        const double epsilon = 0.000001;
        return Math.Abs(firstPoint.Latitude - secondPoint.Latitude) <= epsilon
            && Math.Abs(firstPoint.Longitude - secondPoint.Longitude) <= epsilon;
    }

    private static string GetOfflineRoutePackagePath()
    {
        var folder = Path.Combine(FileSystem.AppDataDirectory, OfflineRouteFolderName);
        return Path.Combine(folder, AppSettings.OfflineRoutePackageFileName);
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

    private static double CalculatePolylineDistanceMeters(IEnumerable<RoutePointModel> coordinates)
    {
        var points = coordinates as IList<RoutePointModel> ?? coordinates.ToList();
        if (points.Count < 2)
            return 0;

        var distanceMeters = 0d;
        for (var index = 1; index < points.Count; index++)
        {
            distanceMeters += CalculateHaversineDistanceMeters(
                points[index - 1].Latitude,
                points[index - 1].Longitude,
                points[index].Latitude,
                points[index].Longitude);
        }

        return distanceMeters;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private sealed class OfflineRoutePackage
    {
        public int GraphVersion { get; set; }
        public List<OfflineRouteNode> Nodes { get; set; } = new();
        public List<OfflineRouteEdge> Edges { get; set; } = new();
    }

    private sealed class OfflineRouteNode
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class OfflineRouteEdge
    {
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }
        public double? DistanceMeters { get; set; }
        public bool Bidirectional { get; set; } = true;
        public List<RoutePointModel> Shape { get; set; } = new();
    }

    private sealed class OfflineRouteGraph
    {
        public OfflineRouteGraph(
            Dictionary<int, OfflineRouteNode> nodes,
            Dictionary<int, List<OfflineAdjacencyEdge>> adjacency)
        {
            Nodes = nodes;
            Adjacency = adjacency;
        }

        public Dictionary<int, OfflineRouteNode> Nodes { get; }
        public Dictionary<int, List<OfflineAdjacencyEdge>> Adjacency { get; }
    }

    private sealed class OfflineAdjacencyEdge
    {
        public OfflineAdjacencyEdge(int toNodeId, double distanceMeters, IReadOnlyList<RoutePointModel> shape)
        {
            ToNodeId = toNodeId;
            DistanceMeters = distanceMeters;
            Shape = shape;
        }

        public int ToNodeId { get; }
        public double DistanceMeters { get; }
        public IReadOnlyList<RoutePointModel> Shape { get; }
    }

    private sealed class NodeCandidate
    {
        public NodeCandidate(int nodeId, double connectorDistanceMeters)
        {
            NodeId = nodeId;
            ConnectorDistanceMeters = connectorDistanceMeters;
        }

        public int NodeId { get; }
        public double ConnectorDistanceMeters { get; }
    }

    private sealed class PathSearchResult
    {
        public PathSearchResult(
            Dictionary<int, double> distances,
            Dictionary<int, (int ParentNodeId, OfflineAdjacencyEdge Edge)> previousEdges)
        {
            Distances = distances;
            PreviousEdges = previousEdges;
        }

        public Dictionary<int, double> Distances { get; }
        public Dictionary<int, (int ParentNodeId, OfflineAdjacencyEdge Edge)> PreviousEdges { get; }
    }
}