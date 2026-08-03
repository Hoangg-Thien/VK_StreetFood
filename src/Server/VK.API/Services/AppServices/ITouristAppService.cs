using VK.API.Common;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public interface ITouristAppService
{
    Task<TouristDto> RegisterTouristAsync(RegisterTouristRequest request);
    Task<ServiceResult<UpdateLocationResultDto>> UpdateLocationAsync(int touristId, UpdateLocationRequest request);
    Task<ServiceResult> LogVisitAsync(int touristId, LogVisitRequest request);
    Task<IReadOnlyList<VisitHistoryDto>> GetVisitHistoryAsync(int touristId);
    Task<ServiceResult> AddFavoriteAsync(int touristId, AddFavoriteRequest request);
    Task<ServiceResult> RemoveFavoriteAsync(int touristId, int poiId);
    Task<IReadOnlyList<POIListItemDto>> GetFavoritesAsync(int touristId, string languageCode = LanguageConstants.Vietnamese);
    Task<ServiceResult> SubmitRatingAsync(int touristId, SubmitRatingRequest request);
    Task<TouristStatsDto?> GetStatsAsync(int touristId);
}
