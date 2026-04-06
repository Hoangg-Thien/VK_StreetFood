using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITouristApiClient
{
    Task<TouristModel?> RegisterTouristAsync(string deviceId, string preferredLanguage, double? latitude = null, double? longitude = null);
    Task<bool> UpdateLocationAsync(int touristId, double latitude, double longitude);
    Task<bool> LogVisitAsync(int touristId, int poiId, string triggerMethod, double? latitude = null, double? longitude = null);
    Task<List<VisitLogModel>> GetVisitHistoryAsync(int touristId);
    Task<bool> AddFavoriteAsync(int touristId, int poiId);
    Task<bool> RemoveFavoriteAsync(int touristId, int poiId);
    Task<List<POIModel>> GetFavoritesAsync(int touristId, string languageCode = "vi");
    Task<bool> SubmitRatingAsync(int touristId, int poiId, int rating, string? comment = null);
}
