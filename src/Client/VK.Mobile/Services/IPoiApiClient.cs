using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IPoiApiClient
{
    Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi");
    Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi");
    Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi");
    Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi");
}
