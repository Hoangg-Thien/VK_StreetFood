using VK.Mobile.Models;
using VK.Contracts.Responses;

namespace VK.Mobile.Services;

public interface IApiService
{
    Task<TouristModel?> RegisterTouristAsync(string deviceId, string preferredLanguage, double? latitude = null, double? longitude = null);
    Task<bool> UpdateLocationAsync(int touristId, double latitude, double longitude);

    Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi");
    Task<PagedResponse<POIModel>?> GetPagedPOIsAsync(int pageNumber = 1, int pageSize = 50, string? search = null, string languageCode = "vi");
    Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi");
    Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi");
    Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi");

    Task<bool> LogVisitAsync(int touristId, int poiId, string triggerMethod, double? latitude = null, double? longitude = null);
    Task<List<VisitLogModel>> GetVisitHistoryAsync(int touristId);

    Task<bool> AddFavoriteAsync(int touristId, int poiId);
    Task<bool> RemoveFavoriteAsync(int touristId, int poiId);
    Task<List<POIModel>> GetFavoritesAsync(int touristId, string languageCode = "vi");

    Task<AudioContentResult?> GetAudioForPOIAsync(int poiId, string languageCode = "vi");
    Task<AudioContentResult?> RequestOnDemandTtsAsync(int poiId, string languageCode = "vi", CancellationToken ct = default);

    Task<bool> SubmitRatingAsync(int touristId, int poiId, int rating, string? comment = null);

    Task<bool> TrackEventAsync(int? touristId, int poiId, string eventType, string? languageCode = null, int? durationSeconds = null);
    Task<TouristStatsModel?> GetMyStatsAsync(int touristId);
    Task<List<TopPOIModel>> GetTopPOIsAsync(int count = 10);

    Task<List<TourModel>> GetToursAsync(string languageCode = "vi");
    Task<TourModel?> GetTourByIdAsync(int tourId, string languageCode = "vi");
    Task<QrPaymentConfigModel?> GetQrPaymentConfigAsync();

    Task PrepareHotsetAsync(IEnumerable<int> poiIds, string languageCode = "vi", CancellationToken ct = default);
    Task WarmupAsync(string languageCode = "vi", CancellationToken ct = default);
}

public class ApiService : IApiService
{
    private readonly ITouristApiClient _touristClient;
    private readonly IPoiApiClient _poiClient;
    private readonly IAudioApiClient _audioClient;
    private readonly IAnalyticsApiClient _analyticsClient;
    private readonly ITourApiClient _tourClient;
    private readonly ILocalizationApiClient _localizationClient;

    public ApiService(
        ITouristApiClient touristClient,
        IPoiApiClient poiClient,
        IAudioApiClient audioClient,
        IAnalyticsApiClient analyticsClient,
        ITourApiClient tourClient,
        ILocalizationApiClient localizationClient)
    {
        _touristClient = touristClient;
        _poiClient = poiClient;
        _audioClient = audioClient;
        _analyticsClient = analyticsClient;
        _tourClient = tourClient;
        _localizationClient = localizationClient;
    }

    public Task<TouristModel?> RegisterTouristAsync(string deviceId, string preferredLanguage, double? latitude = null, double? longitude = null)
        => _touristClient.RegisterTouristAsync(deviceId, preferredLanguage, latitude, longitude);

    public Task<bool> UpdateLocationAsync(int touristId, double latitude, double longitude)
        => _touristClient.UpdateLocationAsync(touristId, latitude, longitude);

    public Task<List<POIModel>> GetAllPOIsAsync(string? search = null, string languageCode = "vi")
        => _poiClient.GetAllPOIsAsync(search, languageCode);

    public Task<PagedResponse<POIModel>?> GetPagedPOIsAsync(int pageNumber = 1, int pageSize = 50, string? search = null, string languageCode = "vi")
        => _poiClient.GetPagedPOIsAsync(pageNumber, pageSize, search, languageCode);

    public Task<List<POIModel>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = "vi")
        => _poiClient.GetNearbyPOIsAsync(latitude, longitude, radiusKm, languageCode);

    public Task<POIDetailModel?> GetPOIDetailAsync(int poiId, string languageCode = "vi")
        => _poiClient.GetPOIDetailAsync(poiId, languageCode);

    public Task<POIDetailModel?> ScanQRCodeAsync(string qrCode, string languageCode = "vi")
        => _poiClient.ScanQRCodeAsync(qrCode, languageCode);

    public Task<bool> LogVisitAsync(int touristId, int poiId, string triggerMethod, double? latitude = null, double? longitude = null)
        => _touristClient.LogVisitAsync(touristId, poiId, triggerMethod, latitude, longitude);

    public Task<List<VisitLogModel>> GetVisitHistoryAsync(int touristId)
        => _touristClient.GetVisitHistoryAsync(touristId);

    public Task<bool> AddFavoriteAsync(int touristId, int poiId)
        => _touristClient.AddFavoriteAsync(touristId, poiId);

    public Task<bool> RemoveFavoriteAsync(int touristId, int poiId)
        => _touristClient.RemoveFavoriteAsync(touristId, poiId);

    public Task<List<POIModel>> GetFavoritesAsync(int touristId, string languageCode = "vi")
        => _touristClient.GetFavoritesAsync(touristId, languageCode);

    public Task<AudioContentResult?> GetAudioForPOIAsync(int poiId, string languageCode = "vi")
        => _audioClient.GetAudioForPOIAsync(poiId, languageCode);

    public Task<AudioContentResult?> RequestOnDemandTtsAsync(int poiId, string languageCode = "vi", CancellationToken ct = default)
        => _audioClient.RequestOnDemandTtsAsync(poiId, languageCode, ct);

    public Task<bool> SubmitRatingAsync(int touristId, int poiId, int rating, string? comment = null)
        => _touristClient.SubmitRatingAsync(touristId, poiId, rating, comment);

    public Task<bool> TrackEventAsync(int? touristId, int poiId, string eventType, string? languageCode = null, int? durationSeconds = null)
        => _analyticsClient.TrackEventAsync(touristId, poiId, eventType, languageCode, durationSeconds);

    public Task<TouristStatsModel?> GetMyStatsAsync(int touristId)
        => _analyticsClient.GetMyStatsAsync(touristId);

    public Task<List<TopPOIModel>> GetTopPOIsAsync(int count = 10)
        => _analyticsClient.GetTopPOIsAsync(count);

    public Task<List<TourModel>> GetToursAsync(string languageCode = "vi")
        => _tourClient.GetToursAsync(languageCode);

    public Task<TourModel?> GetTourByIdAsync(int tourId, string languageCode = "vi")
        => _tourClient.GetTourByIdAsync(tourId, languageCode);

    public Task<QrPaymentConfigModel?> GetQrPaymentConfigAsync()
        => _touristClient.GetQrPaymentConfigAsync();

    public Task PrepareHotsetAsync(IEnumerable<int> poiIds, string languageCode = "vi", CancellationToken ct = default)
        => _localizationClient.PrepareHotsetAsync(poiIds, languageCode, ct);

    public Task WarmupAsync(string languageCode = "vi", CancellationToken ct = default)
        => _localizationClient.WarmupAsync(languageCode, ct);
}
