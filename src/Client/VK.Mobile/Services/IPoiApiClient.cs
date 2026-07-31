using VK.Mobile.Models;
using VK.Contracts.Responses;

namespace VK.Mobile.Services;

public interface IPoiApiClient
{
    Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi");
    Task<PagedResponse<POIModel>?> GetPagedPOIsAsync(int pageNumber = 1, int pageSize = 50, string? search = null, string languageCode = "vi");
    Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi");
    Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi");
    Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi");
}
