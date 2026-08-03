using VK.Contracts.Responses;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public interface IPOIAppService
{
    Task<IReadOnlyList<POIListItemDto>> GetAllPOIsAsync(int? categoryId = null, string? search = null, string languageCode = LanguageConstants.Vietnamese);
    Task<PagedResponse<POIListItemDto>> GetPagedPOIsAsync(int pageNumber = 1, int pageSize = 50, int? categoryId = null, string? search = null, string languageCode = LanguageConstants.Vietnamese);
    Task<IReadOnlyList<POIListItemDto>> GetNearbyPOIsAsync(double latitude, double longitude, double radiusKm = 1.0, string languageCode = LanguageConstants.Vietnamese);
    Task<POIDetailDto?> GetPOIByIdAsync(int id, string languageCode = LanguageConstants.Vietnamese);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
}
