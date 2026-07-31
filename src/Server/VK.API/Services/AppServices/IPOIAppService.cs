using Microsoft.AspNetCore.Mvc;
using VK.Shared.Constants;

namespace VK.API.Services.AppServices;

public interface IPOIAppService
{
    Task<IActionResult> GetAllPOIsAsync(int? categoryId = null, string? search = null, string languageCode = LanguageConstants.Vietnamese);
    Task<IActionResult> GetPagedPOIsAsync(int pageNumber = 1, int pageSize = 50, int? categoryId = null, string? search = null, string languageCode = LanguageConstants.Vietnamese);
    Task<IActionResult> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = LanguageConstants.Vietnamese);
    Task<IActionResult> GetPOIByIdAsync(int id, string languageCode = LanguageConstants.Vietnamese);
    Task<IActionResult> GetCategoriesAsync();
}
