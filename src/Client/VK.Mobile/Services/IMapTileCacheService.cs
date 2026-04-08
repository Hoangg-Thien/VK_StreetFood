namespace VK.Mobile.Services;

public interface IMapTileCacheService
{
    Task PreCacheMapTilesAsync(CancellationToken ct);
}
