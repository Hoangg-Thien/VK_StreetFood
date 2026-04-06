namespace VK.Mobile.Services;

public class MapTileCacheService : IMapTileCacheService
{
    private readonly ILogger<MapTileCacheService> _logger;

    public MapTileCacheService(ILogger<MapTileCacheService> logger)
    {
        _logger = logger;
    }

    public async Task PreCacheMapTilesAsync(CancellationToken ct)
    {
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

                    var tilePath = Path.Combine(tileCacheDir, zoom.ToString(), x.ToString(), $"{y}.png");
                    if (File.Exists(tilePath) && new FileInfo(tilePath).Length > 0)
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
                        var url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
                        var bytes = await http.GetByteArrayAsync(url, ct);
                        await File.WriteAllBytesAsync(tilePath, bytes, ct);
                        downloaded++;
                        await Task.Delay(100, ct);
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
}
