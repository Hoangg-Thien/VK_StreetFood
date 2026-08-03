using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public interface ITourAppService
{
    Task<IReadOnlyList<TourListItemDto>> GetToursAsync(string languageCode = LanguageConstants.Vietnamese);
    Task<TourDetailDto?> GetTourByIdAsync(int tourId, string languageCode = LanguageConstants.Vietnamese);
}
