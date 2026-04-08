using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;

namespace VK.Infrastructure.Repositories;

public class PoiManagementRepository : IPoiManagementRepository
{
    private readonly VKStreetFoodDbContext _context;

    public PoiManagementRepository(VKStreetFoodDbContext context)
    {
        _context = context;
    }

    public async Task<(List<PointOfInterest> Pois, int Total)> GetPagedPoisAsync(
        string? search,
        int? categoryId,
        bool? isActive,
        int? ownerVendorId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PointsOfInterest
            .Include(p => p.Category)
            .Include(p => p.AudioContents)
            .AsQueryable();

        if (ownerVendorId.HasValue)
        {
            var ownerPoiId = await GetOwnerPoiIdAsync(ownerVendorId.Value, cancellationToken);
            query = ownerPoiId.HasValue ? query.Where(p => p.Id == ownerPoiId.Value) : query.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Address.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var pois = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (pois, total);
    }

    public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => _context.Categories.ToListAsync(cancellationToken);

    public Task<int?> GetOwnerPoiIdAsync(int vendorId, CancellationToken cancellationToken = default)
        => _context.Vendors
            .Where(v => v.Id == vendorId && !v.IsDeleted)
            .Select(v => (int?)v.PointOfInterestId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<User?> GetOwnerUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.Users
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email == email && u.Role == "poi_owner", cancellationToken);

    public Task<PointOfInterest?> GetPoiByIdAsync(int poiId, CancellationToken cancellationToken = default)
        => _context.PointsOfInterest.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);

    public Task AddPoiAsync(PointOfInterest poi, CancellationToken cancellationToken = default)
        => _context.PointsOfInterest.AddAsync(poi, cancellationToken).AsTask();

    public Task<List<PointOfInterestTranslation>> GetTranslationsAsync(int poiId, CancellationToken cancellationToken = default)
        => _context.PointOfInterestTranslations
            .Where(t => t.PointOfInterestId == poiId)
            .ToListAsync(cancellationToken);

    public Task AddTranslationAsync(PointOfInterestTranslation translation, CancellationToken cancellationToken = default)
        => _context.PointOfInterestTranslations.AddAsync(translation, cancellationToken).AsTask();

    public async Task HardDeletePoiGraphAsync(int poiId, CancellationToken cancellationToken = default)
    {
        await _context.VisitLogs
            .IgnoreQueryFilters()
            .Where(v => v.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.Analytics
            .IgnoreQueryFilters()
            .Where(a => a.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.Ratings
            .IgnoreQueryFilters()
            .Where(r => r.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.Favorites
            .IgnoreQueryFilters()
            .Where(f => f.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.TourPointsOfInterest
            .IgnoreQueryFilters()
            .Where(tp => tp.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.PoiContentChangeRequests
            .IgnoreQueryFilters()
            .Where(r => r.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.AudioContents
            .IgnoreQueryFilters()
            .Where(a => a.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.PointOfInterestTranslations
            .IgnoreQueryFilters()
            .Where(t => t.PointOfInterestId == poiId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.PointsOfInterest
            .IgnoreQueryFilters()
            .Where(p => p.Id == poiId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
