using Microsoft.EntityFrameworkCore;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;

namespace VK.Infrastructure.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly VKStreetFoodDbContext _context;
    private readonly DbSet<TEntity> _dbSet;

    public Repository(VKStreetFoodDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<TEntity>();
    }

    public IQueryable<TEntity> Query() => _dbSet.AsQueryable();

    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _dbSet.FindAsync([id], cancellationToken).AsTask();

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => _dbSet.AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => _dbSet.Update(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);
}
