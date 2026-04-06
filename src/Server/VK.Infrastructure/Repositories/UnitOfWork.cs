using VK.Core.Interfaces;
using VK.Infrastructure.Data;

namespace VK.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly VKStreetFoodDbContext _context;

    public UnitOfWork(VKStreetFoodDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
