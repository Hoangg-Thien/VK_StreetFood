using VK.Core.Entities;

namespace VK.Core.Interfaces;

public interface ITourManagementRepository
{
    Task<List<Tour>> GetToursForManagementAsync(string? status, CancellationToken cancellationToken = default);

    Task<List<PointOfInterest>> GetPoisForBuilderAsync(CancellationToken cancellationToken = default);

    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<Tour?> GetTourByIdWithPointsAsync(int id, CancellationToken cancellationToken = default);

    Task AddTourAsync(Tour tour, CancellationToken cancellationToken = default);

    Task<List<TourPointOfInterest>> GetTourPointsAsync(int tourId, CancellationToken cancellationToken = default);

    Task AddTourPointAsync(TourPointOfInterest point, CancellationToken cancellationToken = default);

    Task<List<TourTranslation>> GetTranslationsAsync(int tourId, CancellationToken cancellationToken = default);

    Task AddTranslationAsync(TourTranslation translation, CancellationToken cancellationToken = default);

    Task HardDeleteTourGraphAsync(int id, CancellationToken cancellationToken = default);
}
