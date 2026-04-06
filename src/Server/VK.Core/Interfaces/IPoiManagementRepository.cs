using VK.Core.Entities;

namespace VK.Core.Interfaces;

public interface IPoiManagementRepository
{
    Task<(List<PointOfInterest> Pois, int Total)> GetPagedPoisAsync(
        string? search,
        int? categoryId,
        bool? isActive,
        int? ownerVendorId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<int?> GetOwnerPoiIdAsync(int vendorId, CancellationToken cancellationToken = default);

    Task<User?> GetOwnerUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<PointOfInterest?> GetPoiByIdAsync(int poiId, CancellationToken cancellationToken = default);

    Task AddPoiAsync(PointOfInterest poi, CancellationToken cancellationToken = default);

    Task<List<PointOfInterestTranslation>> GetTranslationsAsync(int poiId, CancellationToken cancellationToken = default);

    Task AddTranslationAsync(PointOfInterestTranslation translation, CancellationToken cancellationToken = default);

    Task HardDeletePoiGraphAsync(int poiId, CancellationToken cancellationToken = default);
}
