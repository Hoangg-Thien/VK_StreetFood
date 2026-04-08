using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;

namespace VK.Infrastructure.Repositories;

public class TourManagementRepository : ITourManagementRepository
{
    private readonly VKStreetFoodDbContext _context;

    public TourManagementRepository(VKStreetFoodDbContext context)
    {
        _context = context;
    }

    public async Task<List<Tour>> GetToursForManagementAsync(string? status, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = (status ?? string.Empty).Trim().ToLowerInvariant();

        var query = _context.Tours
            .Include(t => t.TourPoints.OrderBy(tp => tp.SortOrder))
            .ThenInclude(tp => tp.PointOfInterest)
            .AsQueryable();

        if (normalizedStatus is "active" or "inactive")
            query = query.Where(t => t.Status == normalizedStatus);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PointOfInterest>> GetPoisForBuilderAsync(CancellationToken cancellationToken = default)
        => _context.PointsOfInterest
            .Include(p => p.Category)
            .Include(p => p.AudioContents)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => _context.Categories.ToListAsync(cancellationToken);

    public Task<Tour?> GetTourByIdWithPointsAsync(int id, CancellationToken cancellationToken = default)
        => _context.Tours
            .Include(t => t.TourPoints)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task AddTourAsync(Tour tour, CancellationToken cancellationToken = default)
        => _context.Tours.AddAsync(tour, cancellationToken).AsTask();

    public Task<List<TourPointOfInterest>> GetTourPointsAsync(int tourId, CancellationToken cancellationToken = default)
        => _context.TourPointsOfInterest
            .Where(tp => tp.TourId == tourId)
            .ToListAsync(cancellationToken);

    public Task AddTourPointAsync(TourPointOfInterest point, CancellationToken cancellationToken = default)
        => _context.TourPointsOfInterest.AddAsync(point, cancellationToken).AsTask();

    public Task<List<TourTranslation>> GetTranslationsAsync(int tourId, CancellationToken cancellationToken = default)
        => _context.TourTranslations
            .Where(t => t.TourId == tourId)
            .ToListAsync(cancellationToken);

    public Task AddTranslationAsync(TourTranslation translation, CancellationToken cancellationToken = default)
        => _context.TourTranslations.AddAsync(translation, cancellationToken).AsTask();

    public async Task HardDeleteTourGraphAsync(int id, CancellationToken cancellationToken = default)
    {
        await _context.TourPointsOfInterest
            .IgnoreQueryFilters()
            .Where(tp => tp.TourId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.TourTranslations
            .IgnoreQueryFilters()
            .Where(t => t.TourId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.Tours
            .IgnoreQueryFilters()
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
