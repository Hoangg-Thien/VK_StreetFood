using Microsoft.AspNetCore.Mvc;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public interface ITouristAppService
{
    Task<IActionResult> RegisterTouristAsync(RegisterTouristRequest request);
    Task<IActionResult> UpdateLocationAsync(int touristId, UpdateLocationRequest request);
    Task<IActionResult> LogVisitAsync(int touristId, LogVisitRequest request);
    Task<IActionResult> GetVisitHistoryAsync(int touristId);
    Task<IActionResult> AddFavoriteAsync(int touristId, AddFavoriteRequest request);
    Task<IActionResult> RemoveFavoriteAsync(int touristId, int poiId);
    Task<IActionResult> GetFavoritesAsync(int touristId, string languageCode = LanguageConstants.Vietnamese);
    Task<IActionResult> SubmitRatingAsync(int touristId, SubmitRatingRequest request);
    Task<IActionResult> GetStatsAsync(int touristId);
}
