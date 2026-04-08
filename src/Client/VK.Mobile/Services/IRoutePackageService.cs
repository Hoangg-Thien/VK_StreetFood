using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IRoutePackageService
{
    Task<bool> EnsureRoutePackageAsync(IReadOnlyList<POIModel> pois, CancellationToken ct);
}
