using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITourApiClient
{
    Task<List<TourModel>> GetToursAsync(string languageCode = "vi");
    Task<TourModel?> GetTourByIdAsync(int tourId, string languageCode = "vi");
}
