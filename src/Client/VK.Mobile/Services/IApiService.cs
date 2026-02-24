using VK.Shared.DTOs;

namespace VK.Mobile.Services;

public interface IApiService
{
    // POI endpoints
    Task<List<PointOfInterestDto>> GetAllPOIsAsync();
    Task<PointOfInterestDto?> GetPOIByIdAsync(int id);
    Task<List<PointOfInterestDto>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0);
    Task<List<CategoryDto>> GetCategoriesAsync();
    
    // Tourist endpoints
    Task<TouristDto?> GetTouristAsync(int id);
    Task<TouristDto> CreateTouristAsync(TouristDto tourist);
    Task<bool> UpdateTouristPreferredLanguageAsync(int touristId, string language);
    
    // Visit endpoints
    Task<bool> RecordVisitAsync(int touristId, int poiId);
    Task<List<VisitDto>> GetTouristVisitsAsync(int touristId);
    
    // Audio endpoints
    Task<AudioDto?> GetAudioAsync(int poiId, string language);
    Task<byte[]> DownloadAudioFileAsync(string audioUrl);
    
    // Rating endpoints
    Task<bool> RateVisitAsync(int visitId, int score, string? comment = null);
}
